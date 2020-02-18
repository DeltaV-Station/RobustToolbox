﻿using System;
using System.Linq;
using System.Reflection;

namespace Robust.Shared.Utility {
    public static class StringHelpers {

        /// <summary>
        ///    Capitalizes the first letter of all words in the given string (first letter and all letters after a space).
        /// </summary>
        public static string CapitalizeAllWords(string input) {
            char[] chars = input.ToCharArray();
            if (chars.Length > 0) {
                chars[0] = Char.ToUpper(chars[0]);
                for (int i = 1; i < input.Length; i++) {
                    if (chars[i-1] == ' ')
                        chars[i] = Char.ToUpper(chars[i]);
                }
            }

            return new string(chars);
        }
    }
}
