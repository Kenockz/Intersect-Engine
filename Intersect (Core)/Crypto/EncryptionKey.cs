﻿using System;
using System.IO;
using System.IO.Compression;

using Intersect.Crypto.Formats;
using Intersect.Memory;

namespace Intersect.Crypto
{
    public abstract partial class EncryptionKey
    {
        protected EncryptionKey(KeyFormat format)
        {
            Format = format;
        }

        public KeyFormat Format { get; }

        public bool Compressed { get; set; }

        protected abstract bool InternalRead(IBuffer buffer);

        protected abstract bool InternalWrite(IBuffer buffer);

        public bool Read(Stream stream)
        {
            var readStream = stream;
            if (Compressed)
            {
                readStream = new GZipStream(stream, CompressionMode.Decompress);
            }

            using (readStream)
            {
                using (var wrapper = new StreamWrapper(readStream))
                {
                    return InternalRead(wrapper);
                }
            }
        }

        public bool Write(Stream stream)
        {
            var buffer = new StreamWrapper(stream);

            buffer.Write((byte) Format);
            buffer.Write(Compressed);

            if (Compressed)
            {
                buffer = new StreamWrapper(new GZipStream(stream, CompressionLevel.Optimal));
            }

            return InternalWrite(buffer);
        }

        public static bool ToStream(EncryptionKey encryptionKey, Stream stream) =>
            encryptionKey.Write(stream);

        public static EncryptionKey FromStream(Stream stream)
        {
            using (var wrapper = new StreamWrapper(stream))
            {
                if (!wrapper.Read(out byte format))
                {
                    throw new EndOfStreamException();
                }

                if (!wrapper.Read(out bool compressed))
                {
                    throw new EndOfStreamException();
                }

                EncryptionKey encryptionKey;

                switch ((KeyFormat) format)
                {
                    case KeyFormat.Aes:
                        encryptionKey = new AesKey();

                        break;

                    case KeyFormat.Rsa:
                        encryptionKey = new RsaKey();

                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }

                encryptionKey.Compressed = compressed;

                if (!encryptionKey.Read(stream))
                {
                    throw new Exception();
                }

                return encryptionKey;
            }
        }

        public static TKey FromStream<TKey>(Stream stream) where TKey : EncryptionKey
        {
            var key = FromStream(stream);

            if (!(key is TKey castedKey))
            {
                throw new InvalidOperationException(
                    $@"Cannot convert from {key.GetType().Name} to {typeof(TKey).Name}."
                );
            }

            return castedKey;
        }
    }
}
