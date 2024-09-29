using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RTLTMPro {
  public static class LigatureFixer {
    private static readonly List<int> _ltrTextHolder = new List<int>(512);
    private static readonly List<int> _startTagTextHolder = new List<int>(512);
    private static readonly List<int> _endTagTextHolder = new List<int>(512);
    private static readonly List<int> _ltrOutput = new List<int>(512);

    private static readonly HashSet<char>
        _mirroredCharsSet = new HashSet<char>(MirroredCharsMaper.MirroredCharsMap.Keys);
    private static int _endTagIndex = 0;
    private static void FlushBufferToOutputReverse(List<int> buffer, FastStringBuilder output) {
      for (int j = 0; j < buffer.Count; j++) {
        output.Append(buffer[buffer.Count - 1 - j]);
      }

      buffer.Clear();
    }

    private static void FlushBufferToOutput(List<int> buffer, FastStringBuilder output,
        bool clear = true) {
      for (int j = 0; j < buffer.Count; j++) {
        output.Append(buffer[j]);
      }

      if (clear) buffer.Clear();
    }

    /// <summary>
    /// Fixes the flow of the text.
    /// </summary>
    public static void Fix(FastStringBuilder input, List<(int, int)> tags, FastStringBuilder output,
        bool farsi, bool fixTextTags, bool preserveNumbers) {
      // Some texts like tags and English words need to be displayed in their original order.
      // This list keeps the characters that their order should be reserved
      // and streams reserved texts into final letters.
      _ltrTextHolder.Clear();
      _startTagTextHolder.Clear();
      _endTagTextHolder.Clear();
      var (inputCharacterType, inputType) =
          MixedTypographer.CharactersTypeDetermination(input, tags, fixTextTags);
      // Tips:
      // Process is invert order, so the export text is invert in default
      for (int i = input.Length - 1; i >= 0; i--) {
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

        if (inputCharacterType[i] == ContextType.Tag) {
          int nextI = i;
          bool isStartTag = true;
          // TagTextHolder is a List that stores tag contents in LTR order
          // The final order of the tag contents needs to be the same RTL order as the Arabic text
          // Therefore, during the entire Text processing,
          // the tag contents must be adjusted to RTL order in the last cell
          _startTagTextHolder.Add(characterAtThisIndex);
          for (int j = i - 1; j >= 0; j--) {
            if (inputCharacterType[j] != ContextType.Tag) {
              if (input.Get(j + 2) == '/')
                isStartTag = false;
              nextI = j;
              break;
            }

            int jChar = input.Get(j);
            _startTagTextHolder.Add(jChar);
            if (j == 0) nextI = -1;
          }

          if (isStartTag) {
            if (i == input.Length - 1 || inputCharacterType[i + 1] == ContextType.RightToLeft) {
              FlushBufferToOutput(_startTagTextHolder, output);
            } else {
              _startTagTextHolder.Reverse();
              _ltrOutput.AddRange(_startTagTextHolder);
              _startTagTextHolder.Clear();
              _ltrOutput.AddRange(_ltrTextHolder);
              _ltrTextHolder.Clear();
              _endTagTextHolder.Reverse();
              _ltrOutput.AddRange(_endTagTextHolder);
              _endTagTextHolder.Clear();
            }
          } else if (nextI == -1 || inputCharacterType[nextI] == ContextType.RightToLeft) {
            FlushBufferToOutputReverse(_ltrTextHolder, output);
            FlushBufferToOutputReverse(_ltrOutput, output);
            FlushBufferToOutput(_startTagTextHolder, output);
          } else {
            _endTagTextHolder.Clear();
            _endTagTextHolder.AddRange(_startTagTextHolder);
            _startTagTextHolder.Clear();
            _ltrOutput.AddRange(_ltrTextHolder);
            _ltrTextHolder.Clear();
            for (int m = 0; m < tags.Count; m++) {
              var (_, end) = tags[m];
              if (i == end) _endTagIndex = m;
            }
            
          }

          i = nextI + 1;
          continue;
        }

        #endregion

        #region process with Punctutaion and Symbol || Mirrored Chars

        if (Char32Utils.IsPunctuation(characterAtThisIndex) 
            || Char32Utils.IsSymbol(characterAtThisIndex) ||
            characterAtThisIndex == ' ') {
          ContextType characterType = inputCharacterType[i];
          if (_mirroredCharsSet.Contains((char)characterAtThisIndex) 
              && characterType == ContextType.RightToLeft) {
            characterAtThisIndex = MirroredCharsMaper.MirroredCharsMap[(char)characterAtThisIndex];
            FlushBufferToOutputReverse(_ltrTextHolder, output);
            FlushBufferToOutputReverse(_ltrOutput, output);
            output.Append(characterAtThisIndex);
            continue;
          }
          // fixed: refer to inputCharacterType to process Character

          if (characterType == ContextType.RightToLeft) {
            // If program executing in there, this character is an RTL character
            if (_endTagTextHolder.Count != 0) {
              SearchForStartTag(input, tags, i);
              if (_startTagTextHolder.Count != 0)
                FlushBufferToOutput(_endTagTextHolder, output, false);
            }
            FlushBufferToOutputReverse(_ltrTextHolder, output);
            FlushBufferToOutput(_startTagTextHolder, output);
            FlushBufferToOutputReverse(_ltrOutput, output);
            FlushBufferToOutput(_endTagTextHolder, output);
            output.Append(characterAtThisIndex);
            continue;
          }

          if (characterType == ContextType.LeftToRight) {
            _ltrTextHolder.Add(characterAtThisIndex);
            continue;
          }

          if (characterType == ContextType.Default) {
            Debug.LogError($"Error Character Type Process,index:{i},Text:{input}," +
                           $"Text char array:{input.ToString().ToCharArray()}");
            if (inputType == ContextType.RightToLeft) {
              // If program executing in there, this character is an RTL character
              if (_endTagTextHolder.Count != 0) {
                SearchForStartTag(input, tags, i);
                if (_startTagTextHolder.Count != 0)
                  FlushBufferToOutput(_endTagTextHolder, output, false);
              }
              FlushBufferToOutputReverse(_ltrTextHolder, output);
              FlushBufferToOutput(_startTagTextHolder, output);
              FlushBufferToOutputReverse(_ltrOutput, output);
              FlushBufferToOutput(_endTagTextHolder, output);
              output.Append(characterAtThisIndex);
              continue;
            } else {
              _ltrTextHolder.Add(characterAtThisIndex);
              continue;
            }
          }
        }

        #endregion

        if (isInMiddle) {
          bool isAfterEnglishChar = Char32Utils.IsEnglishLetter(previousCharacter);
          bool isBeforeEnglishChar = Char32Utils.IsEnglishLetter(nextCharacter);
          bool isAfterNumber = Char32Utils.IsNumber(previousCharacter, preserveNumbers, farsi);
          bool isBeforeNumber = Char32Utils.IsNumber(nextCharacter, preserveNumbers, farsi);
          bool isAfterSymbol = Char32Utils.IsSymbol(previousCharacter);
          bool isBeforeSymbol = Char32Utils.IsSymbol(nextCharacter);

          // For cases where english words and farsi/arabic are mixed.
          // This allows for using farsi/arabic, english and numbers in one sentence.
          // If the space is between numbers,symbols or English words, keep the order
          if (characterAtThisIndex == ' ' &&
              (isBeforeEnglishChar || isBeforeNumber || isBeforeSymbol) &&
              (isAfterEnglishChar || isAfterNumber || isAfterSymbol)) {
            _ltrTextHolder.Add(characterAtThisIndex);
            continue;
          }
        }

        if (Char32Utils.IsLetter(characterAtThisIndex) && 
            !Char32Utils.IsRTLCharacter(characterAtThisIndex) ||
            Char32Utils.IsNumber(characterAtThisIndex, preserveNumbers, farsi)) {
          _ltrTextHolder.Add(characterAtThisIndex);
          continue;
        }

        // Handle surrogates
        if (characterAtThisIndex >= (char)0xD800 &&
            characterAtThisIndex <= (char)0xDBFF ||
            characterAtThisIndex >= (char)0xDC00 && characterAtThisIndex <= (char)0xDFFF) {
          _ltrTextHolder.Add(characterAtThisIndex);
          continue;
        }

        // If program executing in there, this character is an RTL character
        if (_endTagTextHolder.Count != 0) {
          SearchForStartTag(input, tags, i);
          if (_startTagTextHolder.Count != 0)
            FlushBufferToOutput(_endTagTextHolder, output, false);
        }
        FlushBufferToOutputReverse(_ltrTextHolder, output);
        FlushBufferToOutput(_startTagTextHolder, output);
        FlushBufferToOutputReverse(_ltrOutput, output);
        FlushBufferToOutput(_endTagTextHolder, output);

        if (characterAtThisIndex != 0xFFFF &&
            characterAtThisIndex != (int)SpecialCharacters.ZeroWidthNoJoiner) {
          output.Append(characterAtThisIndex);
        }
      }

      FlushBufferToOutputReverse(_ltrTextHolder, output);
      FlushBufferToOutputReverse(_ltrOutput, output);
    }

    private static bool SearchForStartTag(
        FastStringBuilder input, List<(int, int)> tags, int index) {
      if (_endTagIndex == 0) return false;
      var (start, end) = tags[_endTagIndex - 1];
      var previousTag = new FastStringBuilder(500);
      input.Substring(previousTag, start, end - start + 1);
      string previousTagStr = previousTag.ToString();
      string previousTagType = previousTagStr.Substring(1, previousTagStr.IndexOf('=') - 1);
      var tagTextCharList = new List<char>();
      for (int i = 0; i < _endTagTextHolder.Count; i++) {
        tagTextCharList.Add((char)_endTagTextHolder[i]);
      }
      tagTextCharList.Reverse();
      string endTagStr = new string(tagTextCharList.ToArray());
      string endTagType = endTagStr.Substring(2, endTagStr.IndexOf('>') - 2);
      if (previousTagType == endTagType) {
        _startTagTextHolder.Clear();
        for (int i = end; i >= start; i--) {
          _startTagTextHolder.Add(input.Get(i));
        }
        return true;
      }
      _startTagTextHolder.Clear();
      return false;
    }
  }
}
