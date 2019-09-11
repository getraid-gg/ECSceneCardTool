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
        public readonly int FileLength;

        public CardInfo(string name, int pngStartIndex, int pngEndIndex, int fileLength)
        {
            Name = name;
            PngStartIndex = pngStartIndex;
            PngEndIndex = pngEndIndex;
            FileLength = fileLength;
        }

        public CardInfo OffsetStart(int offset)
        {
            return new CardInfo(Name, PngStartIndex + offset, PngEndIndex + offset, FileLength);
        }
    }

    // Skeleton Card loading exceptions for better filtering
    public class SceneLoadException : Exception
    {
        public override string Message => $"{base.Message}: {InnerException.Message}";
        public SceneLoadException(Exception innerException) : base(innerException.GetType().ToString(), innerException) { }
    }

    public class CardLoadException : Exception
    {
        public CardLoadException(string message) : base(message) { }
    }

    public static class CardExtractor
    {
        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }; // PNG header: ‰PNG 0x0D 0x0A 0x1A 0x0A
        private static readonly byte[] PngEndString = { 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 }; // PNG IEND block: IEND followed by the CRC32 for the empty IEND block

        // This is all information that's probably useful but we can ignore it since we're just trying to extract the cards
        private const int CharacterHeaderLength = 0x6b;

        private static readonly byte[] FullnameBytes = System.Text.Encoding.UTF8.GetBytes("fullname");
        private static readonly byte[] SceneDataNameBytes = System.Text.Encoding.UTF8.GetBytes("【EroMakeHScene】");

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

            var characterStart = pngStartPosition + PngSignature.Length;

            List<CardInfo> cardsFound = new List<CardInfo>();

            characterStart = FindByteSequenceIndex(sceneData, PngSignature, characterStart);
            while (characterStart != -1)
            {
                try
                {
                    var cardInfo = ReadCard(sceneData, characterStart);
                    cardsFound.Add(cardInfo);
                    characterStart = characterStart + cardInfo.FileLength;
                }
                catch (CardLoadException e)
                {
                    throw new SceneLoadException(e);
                }
                
                // check if we're at a PNG header to see if there is another card
                characterStart = FindByteSequenceIndex(sceneData, PngSignature, characterStart, PngSignature.Length);
            }

            return cardsFound;
        }

        public static CardInfo ReadCard(byte[] data, int startIndex)
        {
            var pngEndIndex = FindByteSequenceIndex(data, PngEndString, startIndex);
            if (pngEndIndex == -1)
            {
                throw new CardLoadException("Character card data does not contain the PNG end marker.");
            }
            pngEndIndex += PngEndString.Length;

            var dataPosition = pngEndIndex + CharacterHeaderLength;

            // The next chunk of data is just a list of package IDs the card uses preceded by its length
            var packageIDCount = BitConverter.ToInt32(data, dataPosition);
            dataPosition += 4 + packageIDCount * 4;

            var lstInfoLength = BitConverter.ToInt32(data, dataPosition);
            // Advance by one int and by the int we just read
            dataPosition += 4 + lstInfoLength;
            // For some reason, they have the data length written as a long
            // even though their data should never exceed 2GB
            var dataLength = BitConverter.ToInt64(data, dataPosition);
            if (dataLength > int.MaxValue)
            {
                // If this ever happens...
                throw new CardLoadException("Character data is too large (>2GB).");
            }
            // Once we're past this data chunk, we're at the end of the card file
            var dataEnd = dataPosition + 8 + (int)dataLength;

            // Yes, I'm parsing MessagePack data to find the character's name.
            var nameIndex = FindByteSequenceIndex(data, FullnameBytes, pngEndIndex, dataEnd - pngEndIndex);
            if (nameIndex == -1)
            {
                throw new CardLoadException("Failed to find the name index for a character card.");
            }
            nameIndex += FullnameBytes.Length;
            var nameLengthByte = data[nameIndex];
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
                        nameLength = data[nameIndex + 1];
                        nameIndex += 2;
                        break;
                    case 0xda: // str 16 type, big endian
                        nameLength = System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(data, nameIndex + 1));
                        nameIndex += 3;
                        break;
                    case 0xdb: // str 32 type, big endian
                        nameLength = System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt32(data, nameIndex + 1));
                        nameIndex += 5;
                        break;
                    default:
                        throw new SceneLoadException(new Exception($"Unrecognized string format byte: {nameLengthByte.ToString("X2")}."));
                }
            }

            var characterName = System.Text.Encoding.UTF8.GetString(data, nameIndex, nameLength);
            return new CardInfo(characterName, startIndex, pngEndIndex, dataEnd - startIndex);
        }

        /// <summary>
        /// Finds the location of the character card count in the given scene data
        /// </summary>
        /// <param name="sceneData">the scene data in which to find the character card count</param>
        /// <returns>the location of the character card count</returns>
        public static int FindCharacterCardCount(byte[] sceneData)
        {
            var dataStartIndex = FindByteSequenceIndex(sceneData, SceneDataNameBytes, 0);

            var dataPosition = dataStartIndex + SceneDataNameBytes.Length;
            // version string
            SkipString(sceneData, ref dataPosition);
            
            // Next is a bunch of scene metadata
            dataPosition += 4; // int32 language ID

            // user ID, save ID, title, comment - all strings
            SkipString(sceneData, ref dataPosition);
            SkipString(sceneData, ref dataPosition);
            SkipString(sceneData, ref dataPosition);
            SkipString(sceneData, ref dataPosition);
            
            dataPosition += 4; // int32 default BGM

            // Array of scene tags
            SkipIntLengthPrefixedData(sceneData, 4, ref dataPosition);

            // int32 male count, int32 female count, 3 bools = 11 bytes
            dataPosition += 11;

            // Array of character package IDs
            SkipIntLengthPrefixedData(sceneData, 4, ref dataPosition);

            // Array of map package IDs
            SkipIntLengthPrefixedData(sceneData, 4, ref dataPosition);

            // bool flag, int32 map item count?
            dataPosition += 5;

            return dataPosition;
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

            for (var i = startIndex; i < startIndex + length + 1 - sequence.Length; i++)
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

        /// <summary>
        /// Advance the array index by a length-prefixed array of fixed-legnth data
        /// </summary>
        /// <param name="source">the data source</param>
        /// <param name="elementSize">the size of a single array element</param>
        /// <param name="index">the data index to update</param>
        private static void SkipIntLengthPrefixedData(byte[] source, int elementSize, ref int index)
        {
            index += BitConverter.ToInt32(source, index) * elementSize + 4;
        }

        /// <summary>
        /// Advance the array index by a length-prefixed string
        /// </summary>
        /// <param name="source">the data source</param>
        /// <param name="index">the data index to update</param>
        private static void SkipString(byte[] source, ref int index)
        {
            // BinaryWriter string length is 7-bit encoded
            var stringByteCount = 0;
            var lengthByteCount = 0;
            for (var i = 0; i < 5; i++)
            {
                var currentByte = source[index + i];
                stringByteCount |= (0x7F & currentByte) << 7 * i;
                if (currentByte >> 7 == 0)
                {
                    lengthByteCount = i + 1;
                    break;
                }
            }

            index += lengthByteCount + stringByteCount;
        }
    }
}

