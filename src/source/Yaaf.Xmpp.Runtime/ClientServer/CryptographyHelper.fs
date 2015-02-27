// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------          
module Yaaf.Xmpp.CryptographyHelpers
    open System
    open System.IO
    open System.Collections.Generic
    open System.Linq
    open System.Text
    open System.Security.Cryptography
    open System.Diagnostics

    type ushort = System.UInt16
    let inline (!>) (arg:^b) : ^a = (^b : (static member op_Implicit: ^b -> ^a) arg)
    let inline (!?>) (arg:^b) : ^a = (^b : (static member op_Explicit: ^b -> ^a) arg)

    let inline ushort s = (!?> s) : ushort
    
    type PemStringType = 
        | Certificate
        | RsaPrivateKey
        | PrivateKey
    module Helpers = 
        let DecodeIntegerSize(rd:System.IO.BinaryReader ) =
            let mutable byteValue = 0uy
            let mutable count = 0

            byteValue <- rd.ReadByte()
            if (byteValue <> 0x02uy) then     // indicates an ASN.1 integer value follows
                0
            else

            byteValue <- rd.ReadByte()
            if (byteValue = 0x81uy) then
                count <- int <| rd.ReadByte()    // data size is the following byte
            else if (byteValue = 0x82uy) then
                let hi = rd.ReadByte()  // data size in next 2 bytes
                let lo = rd.ReadByte()
                count <- int <| BitConverter.ToUInt16([|lo; hi |], 0)
            else
                count <- int byteValue        // we already have the data size
            

            //remove high order zeros in data
            while (rd.ReadByte() = 0x00uy) do
                count <- count - 1
            rd.BaseStream.Seek(int64 -1, System.IO.SeekOrigin.Current) |> ignore
            count
        let AlignBytes(inputBytes : byte[], alignSize: int) =
            let inputBytesSize = inputBytes.Length

            if ((alignSize <> -1) && (inputBytesSize < alignSize)) then
                let buf = Array.zeroCreate alignSize
                for i in 0 .. inputBytesSize - 1 do // (int i = 0; i < inputBytesSize; ++i)
                    buf.[i + (alignSize - inputBytesSize)] <- inputBytes.[i];
                buf
            else
                inputBytes      // Already aligned, or doesn't need alignment
            
        let GetBytesFromPEM(pemString:string, t:PemStringType) = 
            let key =
                match t with
                | PemStringType.Certificate -> "CERTIFICATE"
                | PemStringType.RsaPrivateKey -> "RSA PRIVATE KEY"
                | PemStringType.PrivateKey -> "PRIVATE KEY"
                | _ -> failwith "unknown type"
            let header = sprintf "-----BEGIN %s-----" key
            let footer = sprintf "-----END %s-----" key
            let start = pemString.IndexOf(header) + header.Length;
            let endIndex = pemString.IndexOf(footer, start) - start;
            Convert.FromBase64String(pemString.Substring(start, endIndex))
    type RSAParameterTraitsData = {
        Mod  : int
        Exp  : int
        D    : int
        P    : int
        Q    : int
        DP   : int
        DQ   : int
        InvQ : int }
    type RSAParameterTraits(modulusLengthInBits:int) =
        let data =
            // The modulus length is supposed to be one of the common lengths, which is the commonly referred to strength of the key,
            // like 1024 bit, 2048 bit, etc.  It might be a few bits off though, since if the modulus has leading zeros it could show
            // up as 1016 bits or something like that.
            let mutable assumedLength = -1
            let logbase = Math.Log(float modulusLengthInBits, float 2)
            if ((int <| Math.Ceiling(logbase)) = (int <| Math.Floor(logbase))) then
                // It's already an even power of 2
                assumedLength <- modulusLengthInBits
            else
                // It's not an even power of 2, so round it up to the nearest power of 2.
                assumedLength <- int (logbase + 1.0)
                assumedLength <- int (Math.Pow(2.0, float assumedLength))
                System.Diagnostics.Debug.Assert(false)  // Can this really happen in the field?  I've never seen it, so if it happens
                // you should verify that this really does the 'right' thing!
            
            match (assumedLength) with
            | 1024 -> 
                {   Mod = 0x80;
                    Exp = -1;
                    D = 0x80;
                    P = 0x40;
                    Q = 0x40;
                    DP = 0x40;
                    DQ = 0x40;
                    InvQ = 0x40; }
            | 2048 ->
                {   Mod = 0x100;
                    Exp = -1;
                    D = 0x100;
                    P = 0x80;
                    Q = 0x80;
                    DP = 0x80;
                    DQ = 0x80;
                    InvQ = 0x80; }
            | 4096 ->
                {   Mod = 0x200;
                    Exp = -1;
                    D = 0x200;
                    P = 0x100;
                    Q = 0x100;
                    DP = 0x100;
                    DQ = 0x100;
                    InvQ = 0x100; }
            | _ -> failwith "Unknown key size"

        member x.size_Mod  = data.Mod
        member x.size_Exp  = data.Exp 
        member x.size_D    = data.D   
        member x.size_P    = data.P   
        member x.size_Q    = data.Q   
        member x.size_DP   = data.DP  
        member x.size_DQ   = data.DQ  
        member x.size_InvQ = data.InvQ
    let DecodeRsaPrivateKey(privateKeyBytes : byte[]) =  // : RSACryptoServiceProvider
        use ms = new MemoryStream(privateKeyBytes)
        use rd = new BinaryReader(ms)
        try
            let mutable byteValue : byte = 0uy
            let mutable shortValue : ushort = 0us
            shortValue <- rd.ReadUInt16();

            match (shortValue) with
            | 0x8130us ->
                // If true, data is little endian since the proper logical seq is 0x30 0x81
                rd.ReadByte() |> ignore //advance 1 byte
            | 0x8230us ->
                rd.ReadInt16() |> ignore  //advance 2 bytes
            | _ ->
                failwithf "Improper ASN.1 format"

            shortValue <- rd.ReadUInt16()
            if (shortValue <> 0x0102us) then // (version number)
                failwithf "Improper ASN.1 format, unexpected version number"
            

            byteValue <- rd.ReadByte()
            if (byteValue <> 0x00uy) then
                failwithf "Improper ASN.1 format"

            // The data following the version will be the ASN.1 data itself, which in our case
            // are a sequence of integers.

            // In order to solve a problem with instancing RSACryptoServiceProvider
            // via default constructor on .net 4.0 this is a hack
            let parms = new CspParameters()
            parms.Flags <- CspProviderFlags.NoFlags
            parms.KeyContainerName <- Guid.NewGuid().ToString().ToUpperInvariant()
            parms.ProviderType <- 
                if ((Environment.OSVersion.Version.Major > 5) || ((Environment.OSVersion.Version.Major = 5) && (Environment.OSVersion.Version.Minor >= 1))) then
                    0x18 
                else 1

            let rsa = new RSACryptoServiceProvider(parms)
            let mutable rsAparams = new RSAParameters()

            let readCount = Helpers.DecodeIntegerSize(rd)
            rsAparams.Modulus <- rd.ReadBytes(readCount)

            // Argh, this is a pain.  From emperical testing it appears to be that RSAParameters doesn't like byte buffers that
            // have their leading zeros removed.  The RFC doesn't address this area that I can see, so it's hard to say that this
            // is a bug, but it sure would be helpful if it allowed that. So, there's some extra code here that knows what the
            // sizes of the various components are supposed to be.  Using these sizes we can ensure the buffer sizes are exactly
            // what the RSAParameters expect.  Thanks, Microsoft.
            let traits = new RSAParameterTraits(rsAparams.Modulus.Length * 8)

            rsAparams.Modulus <- Helpers.AlignBytes(rsAparams.Modulus, traits.size_Mod);
            rsAparams.Exponent <- Helpers.AlignBytes(rd.ReadBytes(Helpers.DecodeIntegerSize(rd)), traits.size_Exp);
            rsAparams.D <- Helpers.AlignBytes(rd.ReadBytes(Helpers.DecodeIntegerSize(rd)), traits.size_D);
            rsAparams.P <- Helpers.AlignBytes(rd.ReadBytes(Helpers.DecodeIntegerSize(rd)), traits.size_P);
            rsAparams.Q <- Helpers.AlignBytes(rd.ReadBytes(Helpers.DecodeIntegerSize(rd)), traits.size_Q);
            rsAparams.DP <- Helpers.AlignBytes(rd.ReadBytes(Helpers.DecodeIntegerSize(rd)), traits.size_DP);
            rsAparams.DQ <- Helpers.AlignBytes(rd.ReadBytes(Helpers.DecodeIntegerSize(rd)), traits.size_DQ);
            rsAparams.InverseQ <- Helpers.AlignBytes(rd.ReadBytes(Helpers.DecodeIntegerSize(rd)), traits.size_InvQ);

            rsa.ImportParameters(rsAparams)
            rsa
        //with
        //| exn ->
        //    Debug.Assert(false);
        //    return null;
        finally
            rd.Close()
    
    let CompareBytearrays(a : byte [], b : byte[]) = 
        if(a.Length <> b.Length) then false
        else
        [ 0 .. a.Length - 1 ]
            |> Seq.forall (fun i -> a.[i] = b.[i])

    let DecodePrivateKeyInfo ( pkcs8 : byte []) = 
        // encoded OID sequence for  PKCS #1 rsaEncryption szOID_RSA_RSA = "1.2.840.113549.1.1.1"
        // this byte[] includes the sequence byte and terminal encoded null 
        let SeqOID = [| 0x30uy; 0x0Duy; 0x06uy; 0x09uy; 0x2Auy; 0x86uy; 0x48uy; 0x86uy; 
                        0xF7uy; 0x0Duy; 0x01uy; 0x01uy; 0x01uy; 0x05uy; 0x00uy; |]
        let mutable _seq = Array.zeroCreate 15
        // ---------  Set up stream to read the asn.1 encoded SubjectPublicKeyInfo blob  ------
        use mem = new MemoryStream (pkcs8)
        let lenstream = int mem.Length
        let binr = new BinaryReader (mem)    //wrap Memory Stream with BinaryReader for easy reading
        let mutable bt = 0uy
        let mutable twobytes = 0us
        
        try
            twobytes <- binr.ReadUInt16 ();
            if (twobytes = 0x8130us) then//data read as little endian order (actual data order for Sequence is 30 81)
                binr.ReadByte () |> ignore    //advance 1 byte
            elif (twobytes = 0x8230us) then
                binr.ReadInt16 () |> ignore   //advance 2 bytes
            else
                failwith "unknown exn"
                //return null
            
            
            bt <- binr.ReadByte ()
            if (bt <> 0x02uy) then
                failwith "unknown exn"
                //return null;
            
            twobytes <- binr.ReadUInt16 ()
            
            if (twobytes <> 0x0001us) then
                failwith "unknown exn"
                //return null;
            
            _seq <- binr.ReadBytes (15)   //read the Sequence OID
            if (not <| CompareBytearrays (_seq, SeqOID)) then //make sure Sequence for OID is correct
                failwith "Sequence for OID is incorrect"
                //return null;
            
            bt <- binr.ReadByte ()
            if (bt <> 0x04uy) then //expect an Octet string 
                failwith "expected an Octet string "
                // return null;
            
            bt <- binr.ReadByte ()   //read next byte, or next 2 bytes is  0x81 or 0x82; otherwise bt is the byte count
            if (bt = 0x81uy) then
                binr.ReadByte () |> ignore
            else
                if (bt = 0x82uy) then
                    binr.ReadUInt16 () |> ignore
            //------ at this stage, the remaining sequence should be the RSA private key
            
            let rsaprivkey = binr.ReadBytes ((lenstream - int mem.Position))
            let rsacsp = DecodeRsaPrivateKey (rsaprivkey) // DecodeRSAPrivateKey (rsaprivkey)
            rsacsp
        //} catch (Exception) {
        //    return null;
        finally 
            binr.Close ()
