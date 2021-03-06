﻿module NaiveSearch

open Brahma.Helpers
open OpenCL.Net
open Brahma.OpenCL
open Brahma.FSharp.OpenCL.Core
open Microsoft.FSharp.Quotations
open Brahma.FSharp.OpenCL.Extensions
open System.Collections.Generic

let random = new System.Random()

let computeTemplateLengths templates maxTemplateLength =
    TemplatesGenerator.computeTemplateLengths templates maxTemplateLength 1uy

let generateInput length =
    Array.init length (fun _ -> (byte) (random.Next(255)))

let computeTemplatesSum templates (templateLengths:array<byte>) =
    TemplatesGenerator.computeTemplatesSum templates templateLengths

let generateTemplates templatesSum =
    TemplatesGenerator.generateTemplates templatesSum

let buildSyntaxTree templates maxTemplateLength (templateLengths:array<byte>) (templateArr:array<byte>) =
    let next = Array.init (templates * (int) maxTemplateLength) (fun _ -> Array.init 256 (fun _ -> -1s))
    let leaf = Array.init (templates * (int) maxTemplateLength) (fun _ -> -1s)
    let prefix = Array.init (templates * (int) maxTemplateLength) (fun _ -> -1s)

    let mutable vertices = 0s
    let mutable templateBase = 0

    for n in 0..(templates - 1) do
        let mutable v = 0
        for i in 0..((int) templateLengths.[n] - 1) do
            if next.[v].[(int) templateArr.[templateBase + i]] < 0s then
                vertices <- vertices + 1s
                next.[v].[(int) templateArr.[templateBase + i]] <- vertices
            v <- (int) next.[v].[(int) templateArr.[templateBase + i]]

            if leaf.[v] >= 0s then
                prefix.[n] <- leaf.[v]
        leaf.[v] <- (int16) n
        templateBase <- templateBase + (int) templateLengths.[n]

    prefix, next, leaf, vertices

let findPrefixes templates maxTemplateLength (templateLengths:array<byte>) (templateArr:array<byte>) =
    let prefix, _, _, _ = buildSyntaxTree templates maxTemplateLength templateLengths templateArr

    prefix

let countMatches (result:array<int16>) maxTemplateLength bound length (templateLengths:array<byte>) (prefix:array<int16>) =
    let mutable matches = 0
    let clearBound = min (bound - 1) (length - (int) maxTemplateLength)

    for i in 0..clearBound do
        let mutable matchIndex = result.[i]
        if matchIndex >= 0s then
            matches <- matches + 1

    for i in (clearBound + 1)..(bound - 1) do
        let mutable matchIndex = result.[i]
        while matchIndex >= 0s && i + (int) templateLengths.[(int) matchIndex] > length do
            matchIndex <- prefix.[(int) matchIndex]
            
        if matchIndex >= 0s then
            matches <- matches + 1
    matches

let filterMatches (result:array<int16>) (maxTemplateLength:byte) bound length (templateLengths:array<byte>) (prefix:array<int16>) (dictionary:Dictionary<int, int16>) =
    let mutable matches = 0
    let clearBound = min (bound - 1) (length - (int) maxTemplateLength)

    for i in 0..clearBound do
        let mutable matchIndex = result.[i]
        if matchIndex >= 0s then
            dictionary.Add(i, matchIndex)
            matches <- matches + 1

    for i in (clearBound + 1)..(bound - 1) do
        let mutable matchIndex = result.[i]
        while matchIndex >= 0s && i + (int) templateLengths.[(int) matchIndex] > length do
            matchIndex <- prefix.[(int) matchIndex]
            
        if matchIndex >= 0s then
            dictionary.Add(i, matchIndex)
            matches <- matches + 1
    matches
    
let label = ".NET/Naive"

let findMatches length templates (templateLengths:array<byte>) (cpuArr:array<byte>) (templateArr:array<byte>) =
    Timer<string>.Global.Start()

    let result = Array.init length (fun _ -> -1s)
    for i in 0 .. (length - 1) do
        let mutable templateBase = 0
        for n in 0..(templates - 1) do
            if n > 0 then templateBase <- templateBase + (int) templateLengths.[n - 1]
                        
            let templateEnd = templateBase + (int) templateLengths.[n]
            if i + (int) templateLengths.[n] <= length then
                let mutable matches = true

                let mutable j = 0
                while (matches && templateBase + j < templateEnd) do
                    if cpuArr.[i + j] <> templateArr.[templateBase + j] then
                        matches <- false

                    j <- j + 1

                if matches then result.[i] <- (int16) n
    Timer<string>.Global.Lap(label)
    result

let Main () =
    let length = 3000000

    let maxTemplateLength = 32uy
    let templates = 512

    let templateLengths = computeTemplateLengths templates maxTemplateLength
    let cpuArr = generateInput length

    let templatesSum = computeTemplatesSum templates templateLengths
    let templateArr = generateTemplates templatesSum

    let prefix = findPrefixes templates maxTemplateLength templateLengths templateArr

    printfn "Finding substrings in string with length %A, using %A..." length label
    let matches = countMatches (findMatches length templates templateLengths cpuArr templateArr) maxTemplateLength length length templateLengths prefix
    printfn "done."

    printfn "Found: %A" matches
    printfn "Avg. time, %A: %A" label (Timer<string>.Global.Average(label))

    ignore (System.Console.Read())

//do Main()