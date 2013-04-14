﻿// --------------------------------------------------------------------------------------
// Provides tool tips with F# hints for MonoDevelop
// (this file implements MonoDevelop interfaces and calls 'LanguageService')
// --------------------------------------------------------------------------------------
namespace MonoDevelop.FSharp

open System
open System.Diagnostics
open System.Text
open System.Linq
open System.Collections.Generic
open System.Threading

open Mono.TextEditor
open MonoDevelop.Core
open MonoDevelop.Ide
open MonoDevelop.Ide.CodeCompletion
open MonoDevelop.Ide.Gui
open MonoDevelop.Ide.Gui.Content
open MonoDevelop.Ide.TypeSystem
open MonoDevelop.Refactoring

open ICSharpCode.NRefactory
open ICSharpCode.NRefactory.Semantics
open ICSharpCode.NRefactory.TypeSystem

open Microsoft.FSharp.Compiler.SourceCodeServices

/// Result of F# identifier resolution at the specified location stores the data tip that we 
/// get from the language service (this is passed to MonoDevelop, which asks about tooltip later)
///
/// We resolve language items to an under-specified LocalResolveResult (with an additional field for
/// the data tip text). Goto-definition can operate on the LocalResolveResult.
// 
// Other interesting ResolveResult's we may one day want to return are:
// 
// -- MemberResolveResult
// -- MethodGroupResolveResult
// -- TypeResolveResult
//
// or to return complete IEntity data.
//
// Also of interest is FindReferencesHandler.FindRefs (obj), for find-all-references and
// renaming.
type internal FSharpLocalResolveResult(tip:DataTipText, ivar:IVariable) = 
  inherit LocalResolveResult(ivar)
  member x.DataTip = tip

[<AllowNullLiteral>] 
type internal FSharpLanguageItemWindow(tooltip: string) as this = 
    inherit MonoDevelop.Components.TooltipWindow() 
    let isEmpty = System.String.IsNullOrEmpty (tooltip)|| tooltip = "?"
      
    let label = 
       if isEmpty then null else 
       new MonoDevelop.Components.FixedWidthWrapLabel(Wrap = Pango.WrapMode.WordChar, 
                                                      Indent = -20,BreakOnCamelCasing = true,
                                                      BreakOnPunctuation = true,
                                                      Markup = tooltip )

    let updateFont (label:MonoDevelop.Components.FixedWidthWrapLabel) =
      if (label <> null) then
          label.FontDescription <- MonoDevelop.Ide.Fonts.FontService.GetFontDescription ("LanguageTooltips")
        
    do 
        if not isEmpty then 
          this.BorderWidth <- 3u
          this.Add label
          updateFont label
          this.EnableTransparencyControl <- true
      
    //return the real width
    member this.SetMaxWidth (maxWidth) =
      match this.Child with 
      | :? MonoDevelop.Components.FixedWidthWrapLabel as label -> 
          label.MaxWidth <- maxWidth
          label.RealWidth
      | _ -> this.Allocation.Width 

    override this.OnStyleSet (previousStyle: Gtk.Style) =
      base.OnStyleSet previousStyle
      match this.Child with 
      | :? MonoDevelop.Components.FixedWidthWrapLabel as label -> updateFont label
      | _ -> ()

type FSharpLanguageItemTooltipProvider() = 
    inherit Mono.TextEditor.TooltipProvider()
    
    override x.GetItem (editor, offset) = 
            let extEditor = editor :?> MonoDevelop.SourceEditor.ExtensibleTextEditor 
            let (resolveResult, region) = extEditor.GetLanguageItem (offset)
            if (resolveResult = null) then null else
                let segment = new TextSegment (editor.LocationToOffset (region.BeginLine, region.BeginColumn), region.EndColumn - region.BeginColumn)
                TooltipItem (resolveResult, segment)

    override x.CreateTooltipWindow (editor, offset, modifierState, item : Mono.TextEditor.TooltipItem) = 
            let doc = IdeApp.Workbench.ActiveDocument
            if (doc = null) then null else
            match item.Item with 
            | :? FSharpLocalResolveResult as titem -> 
                let tooltip = TipFormatter.formatTipWithHeader(titem.DataTip)               
                let result = new TooltipInformationWindow(ShowArrow = true)
                let toolTipInfo = new TooltipInformation(SignatureMarkup = tooltip)
                result.AddOverload(toolTipInfo)
                result.RepositionWindow ()                  
                result :> Gtk.Window
            | _ -> Debug.WriteLine("** not a FSharpLocalResolveResult!"); null

    override x.GetRequiredPosition (editor, tipWindow : Gtk.Window, requiredWidth : int byref, xalign : double byref) = 
            match tipWindow with 
            | :? TooltipInformationWindow as win -> 
                requiredWidth <- win.Allocation.Width
                xalign <- 0.5
            | _ -> ()
    
/// Implements "resolution" - looks for tool-tips at current locations
type FSharpResolverProvider() =
  do Debug.WriteLine (sprintf "Resolver: Creating FSharpResolverProvider")
  // TODO: ITextEditorMemberPositionProvider
  // TODO: ITextEditorExtension
  // TODO: MonoDevelop.Ide.Gui.Content.CompletionTextEditorExtension (Parameter completion etc.)
  
  interface ITextEditorResolverProvider with
  
    /// Get tool-tip at the specified offset (from the start of the file)
    member x.GetLanguageItem(doc:Document, offset:int, region:DomRegion byref) : ResolveResult =

      try 
        Debug.WriteLine (sprintf "Resolver: In GetLanguageItem")
        if doc.Editor = null || doc.Editor.Document = null then null else

        let docText = doc.Editor.Text

        if docText = null || offset >= docText.Length || offset < 0 then null else

        let config = IdeApp.Workspace.ActiveConfiguration

        if config = null then null else

        Debug.WriteLine(sprintf "Resolver: Getting results of type checking")
        // Try to get typed result - with the specified timeout
        let tyRes = LanguageService.Service.GetTypedParseResult(new FilePath(doc.Editor.FileName), docText, doc.Project, config, timeout = ServiceSettings.blockingTimeout)
            
        Debug.WriteLine (sprintf "Resolver: Getting tool tip")
        // Get tool-tip from the language service
        let tip = tyRes.GetToolTip(offset, doc.Editor.Document)
        match tip with
        | DataTipText(elems) when elems |> List.forall (function DataTipElementNone -> true | _ -> false) -> 
            Debug.WriteLine (sprintf "Resolver: No data found")
            null
        | _ -> 
            Debug.WriteLine (sprintf "Resolver: Got data")
            // This is the NRefactory symbol for the item - the Region is used for goto-definition
            let ivar = 
                { new IVariable with 
                    member x.Name = "item--item"
                    member x.Region = 
                       Debug.WriteLine("getting declaration location...")
                       // Get the declaration location from the language service
                       let loc = tyRes.GetDeclarationLocation(offset, doc.Editor.Document)
                       match loc with 
                       | DeclFound(line,col,file) -> 
                           Debug.WriteLine("found, line = {0}, col = {1}, file = {2}", line, col, file)
                           DomRegion(file,line+1,col+1)
                       | _ -> DomRegion.Empty
                    member x.Type = (SpecialType.UnknownType :> _)
                    member x.IsConst = false
                    member x.ConstantValue = Unchecked.defaultof<_> }
                    
            new FSharpLocalResolveResult(tip, ivar) :> ResolveResult
      with exn -> 
        Debug.WriteLine (sprintf "Resolver: Exception: '%s'" (exn.ToString()))
        null

    member x.GetLanguageItem(doc:Document, offset:int, identifier:string) : ResolveResult =
      do Debug.WriteLine (sprintf "Resolver: in GetLanguageItem#2")
      let (result, region) = (x :> ITextEditorResolverProvider).GetLanguageItem(doc, offset)
      result

    /// Returns string with tool-tip from 'FSharpLocalResolveResult'
    /// (which we generated in the previous method - so we simply run formatter)
    member x.CreateTooltip(document, offset, result, errorInformation, modifierState) : string = 
      do Debug.WriteLine (sprintf "Resolver: in CreteTooltip")
      match result with
      //| :? FSharpLocalResolveResult as res -> TipFormatter.formatTipWithHeader(res.DataTip)
      | _ -> null
