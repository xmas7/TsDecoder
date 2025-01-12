﻿/* Copyright 2017-2023 Cinegy GmbH.

  Licensed under the Apache License, Version 2.0 (the "License");
  you may not use this file except in compliance with the License.
  You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

  Unless required by applicable law or agreed to in writing, software
  distributed under the License is distributed on an "AS IS" BASIS,
  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
  See the License for the specific language governing permissions and
  limitations under the License.
*/

using System.Text;

namespace Cinegy.TsDecoder.Descriptors
{
    /// <summary>
    /// A Iso 639 Language Descriptor <see cref="Descriptor"/>.
    /// </summary>
    /// <remarks>
    /// For details please refer to the original documentation,
    /// e.g. <i>ETSI EN 300 468 V1.15.1 (2016-03)</i> or alternate versions.
    /// </remarks>
    public class Iso639LanguageDescriptor : Descriptor
    {
        public Iso639LanguageDescriptor(byte[] stream, int start) : base(stream, start)
        {
            Language = Encoding.UTF8.GetString(stream, start + 2, 3);
            AudioType = stream[start + 5];
        }

        public string Language { get; }
        public byte AudioType { get; }
    }
}
