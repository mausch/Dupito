#r "FSharp.PowerPack"

open System
open System.IO
open System.Security.Cryptography

let hashFile f = 
    use fs = new FileStream(f, FileMode.Open)
    use hashFunction = new SHA512Managed()
    hashFunction.ComputeHash fs |> Convert.ToBase64String

let hashAsync bufferSize hashFunction (stream: Stream) =
    let newBuffer() = Array.zeroCreate<byte> bufferSize
    let rec hashBlock currentBlock count (s: Stream) (hash: HashAlgorithm) = async {
        let buffer = newBuffer()
        let! readCount = s.AsyncRead buffer
        if readCount = 0 then
            hash.TransformFinalBlock(currentBlock, 0, count) |> ignore
        else 
            hash.TransformBlock(currentBlock, 0, count, currentBlock, 0) |> ignore
            return! hashBlock buffer readCount s hash
    }
    async {
        let buffer = newBuffer()
        let! readCount = stream.AsyncRead buffer
        do! hashBlock buffer readCount stream hashFunction
        return hashFunction.Hash |> Convert.ToBase64String
    }

let hashFileAsync f =    
    let bufferSize = 32768
    async {
        use! fs = File.AsyncOpenRead f
        use hashFunction = new SHA512Managed()
        hashFunction.Initialize()
        return! hashAsync bufferSize hashFunction fs
    }

