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

using System.Collections.Generic;
using System.Diagnostics;
using Cinegy.TsDecoder.TransportStream;
using System;
using Cinegy.TsDecoder.Descriptors;

namespace Cinegy.TsDecoder.Tables
{
    public class ProgramMapTableFactory : TableFactory
    {
        public ProgramMapTable ProgramMapTable { get; private set; }

        private new ProgramMapTable InProgressTable
        {
            get { return base.InProgressTable as ProgramMapTable; }
            set { base.InProgressTable = value; }
        }

        public void AddPacket(TsPacket packet)
        {
            CheckPid(packet.Pid);

            if (packet.PayloadUnitStartIndicator)
            {
                InProgressTable = new ProgramMapTable { Pid = packet.Pid, PointerField = packet.Payload[0] };

                if (InProgressTable.PointerField > packet.PayloadLen)
                {
                    Debug.Assert(true, "Program Map Table has packet pointer outside the packet.");
                }

                var pos = 1 + InProgressTable.PointerField;

                if (pos > 184)
                {
                    CorruptedPackets++;
                    InProgressTable = null;
                    return;
                }

                InProgressTable.VersionNumber = (byte)((packet.Payload[pos + 5] & 0x3E) >> 1);

                if (ProgramMapTable?.VersionNumber == InProgressTable.VersionNumber)
                {
                    InProgressTable = null;
                    return;
                }

                InProgressTable.TableId = packet.Payload[pos];
                InProgressTable.SectionLength =
                    (ushort)(((packet.Payload[pos + 1] & 0x3) << 8) + packet.Payload[pos + 2]);
                InProgressTable.ProgramNumber = (ushort)((packet.Payload[pos + 3] << 8) + packet.Payload[pos + 4]);
                InProgressTable.CurrentNextIndicator = (packet.Payload[pos + 5] & 0x1) != 0;
                InProgressTable.SectionNumber = packet.Payload[pos + 6];
                InProgressTable.LastSectionNumber = packet.Payload[pos + 7];
                InProgressTable.PcrPid = (ushort)(((packet.Payload[pos + 8] & 0x1f) << 8) + packet.Payload[pos + 9]);
                InProgressTable.ProgramInfoLength = (byte)(((packet.Payload[pos + 10] & 0x3) << 8) + packet.Payload[pos + 11]);

            }

            if (InProgressTable == null) return;

            AddData(packet);

            if (!HasAllBytes()) return;

            if (InProgressTable.ProgramInfoLength > Data.Length)
            {
                CorruptedPackets++;
                InProgressTable = null;
                return;
            }

            var startOfNextField = GetDescriptors(InProgressTable.ProgramInfoLength, InProgressTable.PointerField + 13);

            if (startOfNextField < 0)
            {
                CorruptedPackets++;
                InProgressTable = null;
                return;
            }
            InProgressTable.EsStreams = ReadEsInfoElements(InProgressTable.SectionLength, startOfNextField);

            var crcPos = InProgressTable.PointerField + InProgressTable.SectionLength - 1; //+3 for start, -4 for len CRC = -1

            if (crcPos > Data.Length)
            {
                CorruptedPackets++;
                InProgressTable = null;
                return;

            }
            InProgressTable.Crc = (uint)((Data[crcPos] << 24) + (Data[crcPos+1] <<16) + 
                                         (Data[crcPos + 2] << 8) + Data[crcPos + 3]);
                        
            ProgramMapTable = InProgressTable;

            OnTableChangeDetected();
        }

        private List<EsInfo> ReadEsInfoElements(ushort sectionLength, int startOfNextField)
        {
            var streams = new List<EsInfo>();

            while (startOfNextField < sectionLength)
            {
                var es = new EsInfo
                {
                    StreamType = Data[startOfNextField],
                    ElementaryPid = (ushort)(((Data[startOfNextField + 1] & 0x1f) << 8) + Data[startOfNextField + 2]),
                    EsInfoLength = (ushort)(((Data[startOfNextField + 3] & 0x3) << 8) + Data[startOfNextField + 4])
                };

                es.SourceData = new byte[5 + es.EsInfoLength];
                if (es.SourceData.Length > (Data.Length - startOfNextField))
                {
                    CorruptedPackets++;
                    return null;
                }
                Buffer.BlockCopy(Data, startOfNextField, es.SourceData, 0, es.SourceData.Length);

                var descriptors = new List<Descriptor>();

                startOfNextField = startOfNextField + 5;
                var endOfDescriptors = startOfNextField + es.EsInfoLength;
                while (startOfNextField < endOfDescriptors)
                {
                    var des = DescriptorFactory.DescriptorFromData(Data, startOfNextField);
                    descriptors.Add(des);
                    startOfNextField += (des.DescriptorLength + 2);
                }

                es.Descriptors = descriptors;
                streams.Add(es);
            }

            return streams;
        }

    }
}
