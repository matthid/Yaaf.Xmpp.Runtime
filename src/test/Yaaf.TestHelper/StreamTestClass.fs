// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.TestHelper

type MyStreamTestClass() = 
    inherit MyTestClass()
    
    let provider = StreamTestProvider()
    override this.Setup() = 
        base.Setup()
        provider.Setup()
    
    override this.TearDown() = 
        provider.TearDown()
        base.TearDown()
    
    member x.StreamI1 with get () = provider.StreamI1
    member x.StreamI2 with get () = provider.StreamI2
    member x.FinishStream1() = provider.FinishStream1()
    member x.FinishStream2() = provider.FinishStream2()
    member x.Stream1 with get () = provider.Stream1
    member x.Stream2 with get () = provider.Stream2
    member x.CrossStream1 with get () = provider.CrossStream1
    member x.CrossStream2 with get () = provider.CrossStream2
    member x.Reader1 with get () = provider.Reader1
    member x.Reader2 with get () = provider.Reader2
    member x.Writer1 with get () = provider.Writer1
    member x.Writer2 with get () = provider.Writer2
    member x.Write1(msg : string) = provider.Write1 msg
    member x.Write2(msg : string) = provider.Write2 msg

    //[<Test>]
//member x.``Check StreamTestClass Setup and TearDown`` () = ()