using System;
namespace MonoDevelop.FSharp.Gui
{
	[System.ComponentModel.ToolboxItem(true)]
	public partial class FSharpSettingsWidget : Gtk.Bin
	{
		public FSharpSettingsWidget ()
		{
			this.Build ();
		}
		
		public Gtk.CheckButton CheckInteractiveUseDefault { get { return checkInteractiveUseDefault; } }
    	public Gtk.Button ButtonBrowse { get { return buttonBrowse; } }
    	public Gtk.Entry EntryArguments { get { return entryArguments; } }
	    public Gtk.Entry EntryPath { get { return entryPath; } }
	    public Gtk.FontButton FontInteractive { get { return fontbutton1; } }
		public Gtk.CheckButton MatchThemeCheckBox { get { return matchThemeCheckbox; } }
		public Gtk.HBox ColorsHBox { get { return colorsHBox; } }
	    public Gtk.ColorButton BaseColorButton { get { return baseColorButton; } }
	    public Gtk.ColorButton TextColorButton { get { return textColorButton; } }
		public Gtk.CheckButton CheckCompilerUseDefault { get { return checkCompilerUseDefault; } }
#if ALLOW_LANGUAGE_VERSION_PREFERENCE
    public Gtk.CheckButton PreferFSharp20 { get { return preferFSharp2; } }
#endif
    public Gtk.Button ButtonCompilerBrowse { get { return buttonCompilerBrowse; } }
		public Gtk.Entry EntryCompilerPath { get { return entryCompilerPath; } }
		
	}
}

