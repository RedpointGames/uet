// Copyright (c) Spekt Contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Spekt.TestLogger.Utilities
{
     using System.Text.RegularExpressions;

     public static class StringExtensions
     {
         public static string ReplaceInvalidXmlChar(this string str)
         {
             if (str != null)
             {
                 // From xml spec (http://www.w3.org/TR/xml/#charsets) valid chars:
                 // #x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD] | [#x10000-#x10FFFF]
                 // Following control charset are discouraged:
                 // [#x7F-#x84], [#x86-#x9F], [#xFDD0-#xFDEF],
                 // We are handling only #x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD]
                 // because C# support unicode character in range \u0000 to \uFFFF
                 const string invalidChar = @"([^\x09\x0A\x0D\x20-\uD7FF\uE000-\uFFFD]|[\u007F-\u0084\u0086-\u009F\uFDD0-\uFDEF])";
                 MatchEvaluator evaluator = ReplaceInvalidCharacterWithUniCodeEscapeSequence;
                 return Regex.Replace(str, invalidChar, evaluator);
             }

             return str;
         }

         public static string SubstringAfterDot(this string name)
         {
             if (string.IsNullOrEmpty(name))
             {
                 return string.Empty;
             }

             var idx = name.LastIndexOf('.');
             if (idx != -1)
             {
                 return name.Substring(idx + 1);
             }

             return name;
         }

         public static string SubstringBeforeDot(this string name)
         {
             if (string.IsNullOrEmpty(name))
             {
                 return string.Empty;
             }

             var idx = name.LastIndexOf(".");
             if (idx != -1)
             {
                 return name.Substring(0, idx);
             }

             return string.Empty;
         }

         private static string ReplaceInvalidCharacterWithUniCodeEscapeSequence(Match match)
         {
             char x = match.Value[0];
             return string.Format(@"\u{0:x4}", (ushort)x);
         }
     }
}