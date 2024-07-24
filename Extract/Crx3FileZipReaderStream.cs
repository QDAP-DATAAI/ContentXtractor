namespace ContentXtractor.Extract
{
    /// <summary>
    /// This is just a helper FileStream that offsets the position during <see cref="Seek(long, SeekOrigin)"/> to the start of the zip file section inside the CRX3 file.
    /// It was tested to work only with <see cref="System.IO.Compression.ZipFile.ExtractToDirectory(Stream, string)"/>, other use cases are not guaranteed to work.
    /// </summary>
    public class Crx3FileZipReaderStream : FileStream
    {
        private readonly long offset;

        public Crx3FileZipReaderStream(string path) : base(path, FileMode.Open, FileAccess.Read)
        {
            Span<byte> buffer = stackalloc byte[4]; //4 bytes buffer to read int32 values

            Read(buffer); //magic number
            var magicNumber = BitConverter.ToInt32(buffer);
            if (magicNumber != 0x34327243) // it should be "Cr24"
            {
                throw new InvalidDataException($"Invalid CRX file signature. {magicNumber:x}");
            }

            Read(buffer); //version
            var version = BitConverter.ToInt32(buffer);
            if (version != 3) //we don't know what to do if version is not 3
            {
                throw new InvalidDataException($"Invalid CRX file version. {version}.");
            }

            Read(buffer); //header length
            var headerLength = BitConverter.ToInt32(buffer);

            offset = Position + headerLength;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (offset >= 0 && origin == SeekOrigin.Begin)
                offset += this.offset;

            return base.Seek(offset, origin);
        }
    }
}