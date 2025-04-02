using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RTLTMPro
{
    public static class LigatureFixer
    {
        private static readonly HashSet<char>
            _mirroredCharsSet = new HashSet<char>(MirroredCharsMaper.MirroredCharsMap.Keys);

        private static void FlushBufferToOutputReverse(List<(int, int)> buffer,
            FastStringBuilder output, List<int> outputRedirection)
        {
            for (int j = 0; j < buffer.Count; j++)
            {
                var (character, characterIndex) = buffer[buffer.Count - 1 - j];
                output.Append(character);
                outputRedirection.Add(characterIndex);
            }

            buffer.Clear();
        }

        private static void FlushBufferToOutput(List<(int, int)> buffer, FastStringBuilder output,
            List<int> outputRedirection, bool clear = true)
        {
            for (int j = 0; j < buffer.Count; j++)
            {
                var (character, characterIndex) = buffer[j];
                output.Append(character);
                outputRedirection.Add(characterIndex);
            }

            if (clear) buffer.Clear();
        }

        /// <summary>
        /// Fixes the flow of the text.
        /// </summary>
        public static void Fix(FastStringBuilder input, int[] redirection,
            List<(int, int)> originTags, FastStringBuilder output, bool farsi,
            bool fixTextTags, bool preserveNumbers)
        {
            List<(int, int)> ltrTextHolder = new(512);
            List<(int, int)> startTagTextHolder = new(512);
            List<(int, int)> endTagTextHolder = new(512);
            List<(int, int)> ltrOutput = new(512);
            List<int> outputRedirection = new(512);
            int endTagIndex = 0;
            // Some texts like tags and English words need to be displayed in their original order.
            // This list keeps the characters that their order should be reserved
            // and streams reserved texts into final letters.
            var tags = new List<(int, int)>(originTags.Count);
            for (int i = 0; i < originTags.Count; i++)
            {
                var (start, end) = originTags[i];
                tags.Add((redirection[start], redirection[end]));
            }

            var (inputCharacterType, inputType) =
                MixedTypographer.CharactersTypeDetermination(input, tags, fixTextTags);
            // Tips:
            // Process is invert order, so the export text is invert in default
            for (int i = input.Length - 1; i >= 0; i--)
            {
                bool isInMiddle = i > 0 && i < input.Length - 1;
                bool isAtBeginning = i == 0;
                bool isAtEnd = i == input.Length - 1;

                int characterAtThisIndex = input.Get(i);

                int nextCharacter = default;

                if (!isAtEnd)
                    nextCharacter = input.Get(i + 1);

                int previousCharacter = default;
                if (!isAtBeginning)
                    previousCharacter = input.Get(i - 1);

                #region process with Tags

                if (inputCharacterType[i] == ContextType.Tag)
                {
                    int nextI = i;
                    bool isStartTag = true;
                    // TagTextHolder is a List that stores tag contents in LTR order
                    // The final order of the tag contents needs to be the same RTL order as the Arabic text
                    // Therefore, during the entire Text processing,
                    // the tag contents must be adjusted to RTL order in the last cell
                    startTagTextHolder.Add((characterAtThisIndex, i));
                    for (int j = i - 1; j >= 0; j--)
                    {
                        if (inputCharacterType[j] != ContextType.Tag)
                        {
                            if (input.Get(j + 2) == '/')
                                isStartTag = false;
                            nextI = j;
                            break;
                        }

                        int jChar = input.Get(j);
                        startTagTextHolder.Add((jChar, j));
                        if (j == 0) nextI = -1;
                    }

                    if (isStartTag)
                    {
                        if (i == input.Length - 1 ||
                            inputCharacterType[i + 1] == ContextType.RightToLeft)
                        {
                            FlushBufferToOutput(startTagTextHolder, output, outputRedirection);
                        }
                        else
                        {
                            if (endTagTextHolder.Count == 0)
                            {
                                GenerateEndTag(startTagTextHolder, endTagTextHolder, endTagIndex);
                                FlushBufferToOutput(startTagTextHolder, output, outputRedirection);
                            }

                            startTagTextHolder.Reverse();
                            ltrOutput.AddRange(startTagTextHolder);
                            startTagTextHolder.Clear();
                            ltrOutput.AddRange(ltrTextHolder);
                            ltrTextHolder.Clear();
                            endTagTextHolder.Reverse();
                            ltrOutput.AddRange(endTagTextHolder);
                            endTagTextHolder.Clear();
                        }
                    }
                    else if (nextI == -1 || inputCharacterType[nextI] == ContextType.RightToLeft)
                    {
                        FlushBufferToOutputReverse(ltrTextHolder, output, outputRedirection);
                        FlushBufferToOutputReverse(ltrOutput, output, outputRedirection);
                        FlushBufferToOutput(startTagTextHolder, output, outputRedirection);
                    }
                    else
                    {
                        endTagTextHolder.Clear();
                        endTagTextHolder.AddRange(startTagTextHolder);
                        startTagTextHolder.Clear();
                        ltrOutput.AddRange(ltrTextHolder);
                        ltrTextHolder.Clear();
                        for (int m = 0; m < tags.Count; m++) {
                            var (_, end) = tags[m];
                            if (i == end) endTagIndex = m;
                        }
                    }

                    i = nextI + 1;
                    continue;
                }

                #endregion

                #region process with Punctutaion and Symbol || Mirrored Chars

                if (Char32Utils.IsPunctuation(characterAtThisIndex)
                    || Char32Utils.IsSymbol(characterAtThisIndex)
                    || _mirroredCharsSet.Contains((char)characterAtThisIndex)
                    || characterAtThisIndex == ' ')
                {
                    ContextType characterType = inputCharacterType[i];
                    // fixed: refer to inputCharacterType to process Character

                    if (characterType == ContextType.RightToLeft)
                    {
                        if (_mirroredCharsSet.Contains((char)characterAtThisIndex))
                        {
                            characterAtThisIndex =
                                MirroredCharsMaper.MirroredCharsMap[(char)characterAtThisIndex];
                        }

                        // If program executing in there, this character is an RTL character
                        if (endTagTextHolder.Count != 0)
                        {
                            if (SearchForStartTag(input, tags, startTagTextHolder, endTagTextHolder, endTagIndex))
                                FlushBufferToOutput(endTagTextHolder, output, outputRedirection,
                                    false);
                        }

                        FlushBufferToOutputReverse(ltrTextHolder, output, outputRedirection);
                        FlushBufferToOutput(startTagTextHolder, output, outputRedirection);
                        FlushBufferToOutputReverse(ltrOutput, output, outputRedirection);
                        FlushBufferToOutput(endTagTextHolder, output, outputRedirection);
                        output.Append(characterAtThisIndex);
                        outputRedirection.Add(i);
                        continue;
                    }

                    if (characterType == ContextType.LeftToRight)
                    {
                        ltrTextHolder.Add((characterAtThisIndex, i));
                        continue;
                    }

                    if (characterType == ContextType.Default)
                    {
                        Debug.LogError($"Error Character Type Process,index:{i},Text:{input}," +
                                       $"Text char array:{input.ToString().ToCharArray()}");
                        if (inputType == ContextType.RightToLeft)
                        {
                            // If program executing in there, this character is an RTL character
                            if (endTagTextHolder.Count != 0)
                            {
                                if (SearchForStartTag(input, tags, startTagTextHolder,
                                        endTagTextHolder, endTagIndex))
                                    FlushBufferToOutput(endTagTextHolder, output, outputRedirection,
                                        false);
                            }

                            FlushBufferToOutputReverse(ltrTextHolder, output, outputRedirection);
                            FlushBufferToOutput(startTagTextHolder, output, outputRedirection);
                            FlushBufferToOutputReverse(ltrOutput, output, outputRedirection);
                            FlushBufferToOutput(endTagTextHolder, output, outputRedirection);
                            output.Append(characterAtThisIndex);
                            outputRedirection.Add(i);
                            continue;
                        }
                        else
                        {
                            ltrTextHolder.Add((characterAtThisIndex, i));
                            continue;
                        }
                    }
                }

                #endregion

                if (isInMiddle)
                {
                    bool isAfterEnglishChar = Char32Utils.IsEnglishLetter(previousCharacter);
                    bool isBeforeEnglishChar = Char32Utils.IsEnglishLetter(nextCharacter);
                    bool isAfterNumber =
                        Char32Utils.IsNumber(previousCharacter, preserveNumbers, farsi);
                    bool isBeforeNumber =
                        Char32Utils.IsNumber(nextCharacter, preserveNumbers, farsi);
                    bool isAfterSymbol = Char32Utils.IsSymbol(previousCharacter);
                    bool isBeforeSymbol = Char32Utils.IsSymbol(nextCharacter);

                    // For cases where english words and farsi/arabic are mixed.
                    // This allows for using farsi/arabic, english and numbers in one sentence.
                    // If the space is between numbers,symbols or English words, keep the order
                    if (characterAtThisIndex == ' ' &&
                        (isBeforeEnglishChar || isBeforeNumber || isBeforeSymbol) &&
                        (isAfterEnglishChar || isAfterNumber || isAfterSymbol))
                    {
                        ltrTextHolder.Add((characterAtThisIndex, i));
                        continue;
                    }
                }

                if (Char32Utils.IsLetter(characterAtThisIndex) &&
                    !Char32Utils.IsRTLCharacter(characterAtThisIndex) ||
                    Char32Utils.IsNumber(characterAtThisIndex, preserveNumbers, farsi ||
                        inputCharacterType[i] == ContextType.LeftToRight))
                {
                    ltrTextHolder.Add((characterAtThisIndex, i));
                    continue;
                }

                // Handle surrogates
                if (characterAtThisIndex >= (char)0xD800 &&
                    characterAtThisIndex <= (char)0xDBFF ||
                    characterAtThisIndex >= (char)0xDC00 && characterAtThisIndex <= (char)0xDFFF)
                {
                    ltrTextHolder.Add((characterAtThisIndex, i));
                    continue;
                }

                // If program executing in there, this character is an RTL character
                if (endTagTextHolder.Count != 0)
                {
                    if (SearchForStartTag(input, tags, startTagTextHolder,
                            endTagTextHolder, endTagIndex))
                        FlushBufferToOutput(endTagTextHolder, output, outputRedirection, false);
                }

                FlushBufferToOutputReverse(ltrTextHolder, output, outputRedirection);
                FlushBufferToOutput(startTagTextHolder, output, outputRedirection);
                FlushBufferToOutputReverse(ltrOutput, output, outputRedirection);
                FlushBufferToOutput(endTagTextHolder, output, outputRedirection);

                if (characterAtThisIndex != 0xFFFF &&
                    characterAtThisIndex != (int)SpecialCharacters.ZeroWidthNoJoiner)
                {
                    output.Append(characterAtThisIndex);
                    outputRedirection.Add(i);
                }
            }

            FlushBufferToOutputReverse(ltrTextHolder, output, outputRedirection);
            FlushBufferToOutputReverse(ltrOutput, output, outputRedirection);

            output.Reverse();
            int p = 0;
            int q = outputRedirection.Count - 1;
            int[] inputRedirection = new int[input.Length];
            Array.Fill(inputRedirection, -1);
            for (int i = 0; i < outputRedirection.Count; i++)
            {
                if (outputRedirection[i] == -1)
                {
                    continue;
                }
                inputRedirection[outputRedirection[i]] = outputRedirection.Count - 1 - i;
            }
            for (int i = 0; i < inputRedirection.Length; i++)
            {
                if (inputRedirection[i] == -1)
                {
                    if (i == 0)
                    {
                        inputRedirection[0] = 0;
                    }
                    else
                    {
                        inputRedirection[i] = inputRedirection[i - 1];
                    }
                    inputRedirection[i] = inputRedirection[i - 1];
                }
            }
            for (int i = 0; i < redirection.Length; i++)
            {
                redirection[i] = inputRedirection[redirection[i]];
            }
        }

        private static bool SearchForStartTag(FastStringBuilder input, List<(int, int)> tags,
            List<(int, int)> startTagTextHolder, List<(int, int)> endTagTextHolder, int endTagIndex)
        {
            if (endTagIndex == 0) return false;
            for (int m = 1; m < endTagIndex; m++)
            {
                var (start, end) = tags[endTagIndex - m];
                var previousTag = new FastStringBuilder(500);
                input.Substring(previousTag, start, end - start + 1);
                string previousTagStr = previousTag.ToString();
                string previousTagType =
                    previousTagStr.Substring(1, previousTagStr.IndexOf('=') - 1);
                var endTagHolderReverse = new char [endTagTextHolder.Count];
                for (int i = 0; i < endTagTextHolder.Count; i++)
                {
                    endTagHolderReverse[endTagTextHolder.Count - i - 1] =
                        (char)endTagTextHolder[i].Item1;
                }

                string endTagStr = new string(endTagHolderReverse.ToArray());
                string endTagType = endTagStr.Substring(2, endTagStr.IndexOf('>') - 2);
                if (previousTagType == endTagType)
                {
                    startTagTextHolder.Clear();
                    for (int i = end; i >= start; i--)
                    {
                        startTagTextHolder.Add((input.Get(i), i));
                    }

                    return true;
                }
            }

            startTagTextHolder.Clear();
            return false;
        }

        private static void GenerateEndTag(List<(int, int)> startTagTextHolder,
            List<(int, int)> endTagTextHolder, int endTagIndex)
        {
            if (endTagTextHolder.Count > 0) return;
            int endIndex;
            int indexTagMiddle = -1;
            for (int i = 0; i < startTagTextHolder.Count; i++)
            {
                var (character, index) = startTagTextHolder[i];
                if (character == '=')
                {
                    indexTagMiddle = i;
                    break;
                }
            }

            int indexTagEnd = -1;
            for (int i = 0; i < startTagTextHolder.Count; i++)
            {
                var (character, index) = startTagTextHolder[i];
                if (character == '>')
                {
                    indexTagEnd = i;
                    break;
                }
            }

            if (indexTagMiddle != -1 || indexTagEnd < indexTagMiddle)
            {
                endIndex = indexTagMiddle;
            }
            else
            {
                endIndex = indexTagEnd;
            }

            if (endIndex == -1) return;
            int typeStrLength = startTagTextHolder.Count - endIndex - 2;
            char[] typeStrChars = new char[typeStrLength];
            for (int i = 0; i < typeStrLength; i++)
            {
                typeStrChars[i] = (char)startTagTextHolder[startTagTextHolder.Count - 2 - i].Item1;
            }

            string typeStr = new string(typeStrChars);
            if (ValidTagMapper.ValidTagSet.Contains(typeStr))
            {
                if (typeStr == "alpha")
                {
                    (int, int)[] temp =
                        Array.ConvertAll<char, (int, int)>("<alpha=#FF>".Reverse().ToArray(),
                            c => (c, -1));
                    endTagTextHolder.AddRange(temp);
                }
                else if (typeStr == "size")
                {
                    (int, int)[] temp =
                        Array.ConvertAll<char, (int, int)>("<size=100%>".Reverse().ToArray(),
                            c => (c, -1));
                    endTagTextHolder.AddRange(temp);
                }
                else if (typeStr == "pos" || typeStr == "space" || typeStr == "sprite")
                {
                    endTagTextHolder.AddRange(startTagTextHolder);
                    startTagTextHolder.Clear();
                }
                else
                {
                    string endTagStr = "</" + typeStr + ">";
                    (int, int)[] temp = Array.ConvertAll<char, (int, int)>(
                        endTagStr.Reverse().ToArray(), c =>
                            (c, -1));
                    endTagTextHolder.AddRange(temp);
                }
            }
        }
    }
}