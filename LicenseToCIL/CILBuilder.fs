﻿[<AutoOpen>]
module LicenseToCIL.Builder
open System

type CILBuilder() =
    member inline __.Zero() = Ops.zero
    member inline __.Yield(op : Op<_, _>) = op
    member inline __.Combine(op1, op2) = Ops.combine op1 op2
    member inline __.Delay(x : unit -> Op<_, _>) = x
    member inline __.Run(x : unit -> Op<_, _>) = x()

    member inline __.Bind(_ : Ops.LabelDefinition, con : 'x Ops.Label -> Op<'i, 'o>) : Op<'i, 'o> =
        fun stackin stackout il ->
            let lbl = il.DefineLabel()
            con (Ops.Label lbl) stackin stackout il

    member inline __.Bind(Ops.LocalDefinition ty, con : Ops.Local -> Op<'i, 'o>) : Op<'i, 'o> =
        fun stackin stackout il ->
            let loc = il.DeclareLocal(ty)
            con loc stackin stackout il

    member inline __.Bind(Ops.LocalTemporary ty, con : Ops.Local -> Op<'i, 'o>) : Op<'i, 'o> =
        fun stackin stackout il ->
            let loc, free = il.AllocateLocal(ty)
            try
                con loc stackin stackout il
            finally
                free()

    member inline __.TryFinally(block : unit -> Op<_, _>, fin : unit -> unit) : Op<_, _> =
        try
            block()
        finally
            fin()

    member inline __.TryWith(block : unit -> Op<'i, 'o>, catcher : exn -> Op<'i, 'o>) =
        try
            block()
        with
        | exn -> catcher(exn)

    member inline this.Using(disposable : 'd when 'd :> IDisposable, body : 'd -> Op<'i, 'o>) : Op<'i, 'o> =
        let dispose () =
            match disposable with
            | null -> ()
            | d -> d.Dispose()
        this.TryFinally((fun () -> body disposable), dispose)

    member this.While(predicate : unit -> bool, iter : unit -> Op<'x, 'x>)
        : Op<'x, 'x> =
        fun stack il ->
            let op =
                if predicate() then
                    Ops.combine
                        (iter())
                        (fun () -> this.While(predicate, iter))
                else Ops.zero
            op stack il
  
    member this.For(elems : 'a seq, iter : 'a -> Op<'x, 'x>)
        : Op<'x, 'x> =
        this.Using(elems.GetEnumerator()
            , fun enumerator ->
                this.While((fun () -> enumerator.MoveNext()), fun () -> iter enumerator.Current))

let cil = CILBuilder()