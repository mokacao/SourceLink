﻿module SourceLink.PdbModify

open System.IO
open System.Text
open System.Collections.Generic
open SourceLink
open SourceLink.Extension
open SourceLink.Exception

let createRootPageBytes (pdbStream:PdbStream) =
    use ms = new MemoryStream()
    use bw = new BinaryWriter(ms, Encoding.UTF8, true)
    for page in pdbStream.Pages do
        bw.Write page
    bw.Flush()
    ms.ToArray()

let createRootBytes (root:PdbRoot) =
    use ms = new MemoryStream()
    use bw = new BinaryWriter(ms, Encoding.UTF8, true)
    bw.Write root.Streams.Count
    for stream in root.Streams do
        bw.Write stream.ByteCount
    for stream in root.Streams do
        for page in stream.Pages do
            bw.Write page
    bw.Flush()
    ms.ToArray()

let createInfoBytes (info:PdbInfo) =
    use ms = new MemoryStream()
    use bw = new BinaryWriter(ms, Encoding.UTF8, true)
    bw.Write info.Version
    bw.Write info.Signature
    bw.Write info.Age
    bw.WriteGuid info.Guid
    
    let names = info.StreamToPdbName |> Seq.map (fun (KeyValue(stream, name)) -> name) |> Seq.toArray

    let nameToPosition = Dictionary<string,int>()
    let position = ref 0
    let nameBytes = // utf8 bytes without null
        names
        |> Seq.map (fun name ->
            let bytes = name.Name.ToUtf8
            nameToPosition.Add(name.Name, !position)
            position := !position + bytes.Length + 1
            bytes
        )
        |> Seq.toArray

    bw.Write !position
    let lastStream = ref 0
    for bytes in nameBytes do
        bw.Write bytes
        bw.Write 0x0uy // null char

    bw.Write names.Length
    let nameIndexMax = info.NameIndexMax
    if names.Length > nameIndexMax then
        failwithf "names.Length > nameIndexMax"
    bw.Write nameIndexMax
    
    let flags =
        let flags = Array.create info.FlagCount 0
        for i in 0 .. names.Length-1 do
            let a = i / 32
            let b = 1 <<< (i % 32)
            flags.[a] <- flags.[a] ||| b
        flags
    
    bw.Write info.FlagCount
    for flag in flags do
        bw.Write flag
    bw.Write 0

    for name in names do
        let position = nameToPosition.[name.Name]
        bw.Write position
        bw.Write name.Stream

    bw.Write info.Tail

    bw.Flush()
    ms.ToArray()