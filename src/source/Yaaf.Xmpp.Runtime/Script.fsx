// Weitere Informationen zu F# finden Sie unter http://fsharp.net. Im Projekt 'F#-Lernprogramm' finden Sie
// einen Leitfaden zum Programmieren in F#.

open System.IO
open System
open System.Threading;
open System.Net
open System.Net.Sockets

let listener = new TcpListener(IPAddress.Any, 15347)
listener.Start()
let acceptor () =
  while true do
    let client = listener.AcceptTcpClient()
    printfn "Connection registered!"
    let stream = client.GetStream()
    let reader = new StreamReader(stream)
    let writer () =
      let mutable run = true
      while run do
        let data = reader.ReadLine()
        if data = null then run <- false
        else printfn "READ: %s" data
    Thread(writer).Start()

Thread(acceptor).Start()