using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;

namespace ECSceneCardTool
{
    public struct CardInfo
    {
        public readonly string Name;
        public readonly int PngStartIndex;
        public readonly int PngEndIndex;
        public readonly int FileEndIndex;

        public CardInfo(string name, int pngStartIndex, int pngEndIndex, int fileEndIndex)
        {
            Name = name;
            PngStartIndex = pngStartIndex;
            PngEndIndex = pngEndIndex;
            FileEndIndex = fileEndIndex;
        }
    }

    // Skeleton Card loading exceptions for better filtering
    public class SceneLoadException : Exception
    {
        public override string Message => $"{base.Message}: {InnerException.Message}";
        public SceneLoadException(Exception innerException) : base(innerException.GetType().ToString(), innerException) { }
        protected SceneLoadException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }

    public static class CardExtractor
    {
        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }; // PNG header: ‰PNG 0x0D 0x0A 0x1A 0x0A
        private static readonly byte[] PngEndString = { 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 }; // PNG IEND block: IEND followed by the CRC32 for the empty IEND block

        // This is all information that's probably useful but we can ignore it since we're just trying to extract the cards
        private const int CharacterHeaderLength = 0x6b;

        private static readonly byte[] FullnameBytes = System.Text.Encoding.UTF8.GetBytes("fullname");

        /// <summary>
        /// Gets the indices of each card's data in a scene
        /// </summary>
        /// <param name="sceneData">the scene file</param>
        /// <returns>a List containing the CardInfo generated for each card found</returns>
        public static List<CardInfo> GetCardInfos(byte[] sceneData, int dataStartIndex = 0)
        {
            // Search for the PNG signature at the start of the data
            var pngStartPosition = FindByteSequenceIndex(sceneData, PngSignature, dataStartIndex, PngSignature.Length);
            if (pngStartPosition == -1)
            {
                throw new SceneLoadException(new FileFormatException($"File does not begin with the PNG format signature."));
            }

            var searchIndex = pngStartPosition + PngSignature.Length;

            List<CardInfo> cardsFound = new List<CardInfo>();

            var characterStart = FindByteSequenceIndex(sceneData, PngSignature, searchIndex);
            searchIndex = characterStart + PngSignature.Length;
            while (characterStart != -1)
            {
                var pngEndIndex = FindByteSequenceIndex(sceneData, PngEndString, searchIndex);
                pngEndIndex += PngEndString.Length;

                var dataPosition = pngEndIndex + CharacterHeaderLength;

                // The next chunk of data is just a list of package IDs the card uses preceded by its length
                var packageIDCount = BitConverter.ToInt32(sceneData, dataPosition);
                dataPosition += 4 + packageIDCount * 4;

                var lstInfoLength = BitConverter.ToInt32(sceneData, dataPosition);
                // Advance by one int and by the int we just read
                dataPosition += 4 + lstInfoLength;
                // For some reason, they have the data length written as a long
                // even though their data should never exceed 2GB
                var dataLength = BitConverter.ToInt64(sceneData, dataPosition);
                if (dataLength > int.MaxValue)
                {
                    // If this ever happens...
                    throw new SceneLoadException(new Exception("Character data is too large (>2GB)."));
                }
                // Once we're past this data chunk, we're at the end of the card file
                var dataEnd = dataPosition + 8 + (int)dataLength;

                // Yes, I'm parsing MessagePack data to find the character's name.
                var nameIndex = FindByteSequenceIndex(sceneData, FullnameBytes, pngEndIndex, dataEnd - pngEndIndex);
                if (nameIndex == -1)
                {
                    throw new SceneLoadException(new Exception("Failed to find the name index for a character card."));
                }
                nameIndex += FullnameBytes.Length;
                var nameLengthByte = sceneData[nameIndex];
                int nameLength;
                // Parse the first byte of the length value to figure out which number type it is and how to read it
                if ((nameLengthByte & 0b10100000) == 0b10100000)
                {
                    // "fixstr" type, where the 5 least significant bits are the length
                    nameLength = nameLengthByte & 0b00011111;
                    nameIndex += 1;
                }
                else
                {
                    switch (nameLengthByte)
                    {
                        case 0xd9: // str 8 type
                            nameLength = sceneData[nameIndex + 1];
                            nameIndex += 2;
                            break;
                        case 0xda: // str 16 type, big endian
                            nameLength = System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(sceneData, nameIndex + 1));
                            nameIndex += 3;
                            break;
                        case 0xdb: // str 32 type, big endian
                            nameLength = System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt32(sceneData, nameIndex + 1));
                            nameIndex += 5;
                            break;
                        default:
                            throw new SceneLoadException(new Exception($"Unrecognized string format byte: {nameLengthByte.ToString("X2")}."));
                    }
                }

                var characterName = System.Text.Encoding.UTF8.GetString(sceneData, nameIndex, nameLength);
                cardsFound.Add(new CardInfo(characterName, characterStart, pngEndIndex, dataEnd));

                searchIndex = dataEnd;
                // only search in place
                characterStart = FindByteSequenceIndex(sceneData, PngSignature, searchIndex, PngSignature.Length);
            }

            return cardsFound;
        }


        private static int FindByteSequenceIndex(byte[] source, byte[] sequence, int startIndex)
        {
            return FindByteSequenceIndex(source, sequence, startIndex, source.Length - startIndex);
        }

        private static int FindByteSequenceIndex(byte[] source, byte[] sequence, int startIndex, int length)
        {
            if (startIndex < 0)
            {
                throw new ArgumentOutOfRangeException("startIndex cannot be negative.");
            }
            if (length <= 0)
            {
                throw new ArgumentOutOfRangeException("length cannot be zero or negative.");
            }
            if (sequence.Length == 0)
            {
                throw new ArgumentException("sequence cannot be empty.");
            }
            if ((uint)(length + startIndex) > (uint)source.Length)
            {
                throw new ArgumentOutOfRangeException("startIndex and length create a range that exceeds the length of the source array.");
            }

            for (int i = startIndex; i < startIndex + length + 1 - sequence.Length; i++)
            {
                var hasMatch = true;
                for (var j = 0; j < sequence.Length; j++)
                {
                    if (sequence[j] != source[i + j])
                    {
                        hasMatch = false;
                        break;
                    }
                }
                if (hasMatch)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}

