#load "../TestHelpers.fsx"
open TestHelpers
open System.IO
open System

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
File.Delete "malformed.txt"

let p = new FSharpAutoCompleteWrapper()

p.project "Malformed.fsproj"
p.send "quit\n"
let output = p.finalOutput ()
File.WriteAllText("malformed.txt", output)

