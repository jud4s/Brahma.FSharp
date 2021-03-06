﻿// Copyright (c) 2012, 2013 Semyon Grigorev <rsdpisuy@gmail.com>
// All rights reserved.
// 
// The contents of this file are made available under the terms of the
// Eclipse Public License v1.0 (the "License") which accompanies this
// distribution, and is available at the following URL:
// http://www.opensource.org/licenses/eclipse-1.0.php
// 
// Software distributed under the License is distributed on an "AS IS" basis,
// WITHOUT WARRANTY OF ANY KIND, either expressed or implied. See the License for
// the specific language governing rights and limitations under the License.
// 
// By using this software in any fashion, you are agreeing to be bound by the
// terms of the License.

namespace Brahma.FSharp.OpenCL.Translator

open Microsoft.FSharp.Quotations
open  Brahma.FSharp.OpenCL.AST

type FSQuotationToOpenCLTranslator() =
   
    let mainKernelName = "brahmaKernel"
    let brahmaDimensionsTypes = ["_1d";"_2d";"_3d"]
    let brahmaDimensionsTypesPrefix = "brahma.opencl."
    let bdts = brahmaDimensionsTypes |> List.map (fun s -> brahmaDimensionsTypesPrefix + s)
    let buildFullAst vars partialAst (context:TargetContext<_,_>) =
        let formalArgs = 
            vars |> List.filter (fun (v:Var) -> bdts |> List.exists((=) (v.Type.FullName.ToLowerInvariant())) |> not)
            |> List.map 
                (fun v -> 
                    let t = Type.Translate v.Type true None
                    new FunFormalArg<_>(t :? RefType<_> , v.Name, t))
        let mainKernelFun = new FunDecl<_>(true, mainKernelName, new PrimitiveType<_>(Void), formalArgs,partialAst)
        let pragmas = 
            let res = new ResizeArray<_>()
            if context.Flags.enableAtomic then
                res.Add(new CLPragma<_>(CLGlobalInt32BaseAtomics) :> TopDef<_> )
                res.Add(new CLPragma<_>(CLLocalInt32BaseAtomics) :> TopDef<_> )
            List.ofSeq res
        new AST<_>(pragmas @ [mainKernelFun])

    let translate qExpr translatorOptions =
        let rec go expr vars =
            match expr with
            | Patterns.Lambda (v, body) -> go body (v::vars)
            | e -> 
                let body =
                    let b,context =
                        let context = new TargetContext<_,_>()
                        context.Namer.LetIn()
                        context.TranslatorOptions.AddRange translatorOptions
                        vars |> List.iter (fun v -> context.Namer.AddVar v.Name)
                        let newE = e |> QuotationsTransformer.inlineLamdas |> QuotationsTransformer.apply
                        Body.Translate newE context
                    match b  with
                    | :? StatementBlock<Lang> as sb -> sb
                    | :? Statement<Lang> as s -> new StatementBlock<_>(new ResizeArray<_>([s]))
                    | _ -> failwithf "Incorrect function body: %A" b
                    ,context 
                vars, body

            | x -> "Incorrect OpenCL quotation: " + string x |> failwith
        let vars,(partialAst,context) = go qExpr []
        buildFullAst (List.rev vars) (partialAst :> Statement<_>) context
  
    member this.Translate qExpr translatorOptions = 
        let ast = translate qExpr translatorOptions
        ast