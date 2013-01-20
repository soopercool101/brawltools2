﻿using System;
using BrawlLib.SSBBTypes;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;

namespace BrawlLib.SSBB.ResourceNodes
{
    public unsafe class RWSDNode : RSARFileNode
    {
        internal RWSDHeader* Header { get { return (RWSDHeader*)WorkingUncompressed.Address; } }
        public override ResourceType ResourceType { get { return ResourceType.RWSD; } }

        //public string Offset { get { if (RSARNode != null) return ((uint)((VoidPtr)Header - (VoidPtr)RSARNode.Header)).ToString("X"); else return null; } }

        [Category("RWSD")]
        public float Version { get { return _version; } }
        private float _version;

        protected override void GetStrings(LabelBuilder builder)
        {
            //foreach (RWSDDataNode node in Children[0].Children)
            //    builder.Add(0, node._name);
        }

        //Finds labels using LABL block between header and footer, also initializes array
        protected bool GetLabels(int count)
        {
            RWSDHeader* header = (RWSDHeader*)WorkingUncompressed.Address;
            int len = header->_header._length;
            LABLHeader* labl = (LABLHeader*)((int)header + len);

            if ((WorkingUncompressed.Length > len) && (labl->_tag == LABLHeader.Tag))
            {
                _labels = new LabelItem[count];
                count = labl->_numEntries;
                for (int i = 0; i < count; i++)
                {
                    LABLEntry* entry = labl->Get(i);
                    _labels[i] = new LabelItem() { String = entry->Name, Tag = entry->_id };
                }
                return true;
            }

            return false;
        }

        private void ParseBlocks()
        {
            VoidPtr dataAddr = Header;
            int len = Header->_header._length;
            int total = WorkingUncompressed.Length;

            //Look for labl block
            LABLHeader* labl = (LABLHeader*)(dataAddr + len);
            if ((total > len) && (labl->_tag == LABLHeader.Tag))
            {
                int count = labl->_numEntries;
                _labels = new LabelItem[count];
                count = labl->_numEntries;
                for (int i = 0; i < count; i++)
                {
                    LABLEntry* entry = labl->Get(i);
                    _labels[i] = new LabelItem() { String = entry->Name, Tag = entry->_id };
                }
                len += labl->_size;
            }

            //Set data source
            if (total > len)
                _audioSource = new DataSource(dataAddr + len, total - len);
        }
        
        protected override bool OnInitialize()
        {
            base.OnInitialize();

            _version = Header->_header.Version;

            ParseBlocks();

            return true;
        }

        protected override void OnPopulate()
        {
            RSARNode rsar = RSARNode;
            SYMBHeader* symb = null;
            RuintList* soundList = null;
            List<string> soundIndices = null;
            VoidPtr soundOffset = null;
            INFOSoundEntry* sEntry;
            ResourceNode g;
            RWSDHeader* rwsd = Header;
            RWSD_DATAHeader* data = rwsd->Data;
            RuintList* list = &data->_list;
            int count = list->_numEntries;

            if (_fileIndex == 86)
                Console.WriteLine();

            new RWSDDataGroupNode().Initialize(this, Header->Data, Header->_dataLength);
            if (Header->_waveOffset > 0)
                new RWSDSoundGroupNode().Initialize(this, Header->Wave, Header->_waveLength);

            //Get sound info from RSAR (mainly for names)
            if (rsar != null)
            {
                INFOHeader* info = rsar.Header->INFOBlock;
                soundOffset = &rsar.Header->INFOBlock->_collection;

                symb = rsar.Header->SYMBBlock;

                soundList = rsar.Header->INFOBlock->Sounds;
                soundIndices = new List<string>();
                
                //foreach (RSARSoundNode in _rsarSoundEntries)


                //for (int i = 0; i < soundList->_numEntries; i++)
                //    for (int x = 0; x < info->Sounds->_numEntries; x++)
                //        if ((sEntry = info->GetSound(i))->_fileId == _fileIndex && sEntry->_soundType == 3)
                //            soundIndices[((WaveSoundInfo*)sEntry->GetSoundInfoRef(soundOffset))->_soundIndex] = sEntry;
            }

            for (int i = 0; i < count; i++)
            {
                RWSD_DATAEntry* entry = (RWSD_DATAEntry*)list->Get(list, i);
                RWSDDataNode node = new RWSDDataNode();
                node._offset = list;
                node.Initialize(Children[0], entry, 0);

                //Attach from INFO block
                //if (soundIndices != null && (sEntry = soundIndices[i]) != null)
                //    node._name = symb->GetStringEntry(sEntry->_stringId);
            }

            //if (soundIndices != null)
            //    Marshal.FreeHGlobal((IntPtr)soundIndices);

            //Get labels
            RSARNode parent;
            int count2 = Header->Data->_list._numEntries;
            if ((_labels == null) && ((parent = RSARNode) != null))
            {
                _labels = new LabelItem[count2];

                //Get them from RSAR
                SYMBHeader* symb2 = parent.Header->SYMBBlock;
                INFOHeader* info = parent.Header->INFOBlock;

                VoidPtr offset = &info->_collection;
                RuintList* soundList2 = info->Sounds;
                count2 = soundList2->_numEntries;

                INFOSoundEntry* entry;
                for (uint i = 0; i < count2; i++)
                    if ((entry = (INFOSoundEntry*)soundList2->Get(offset, (int)i))->_fileId == _fileIndex)
                        _labels[((WaveSoundInfo*)entry->GetSoundInfoRef(offset))->_soundIndex] = new LabelItem() { Tag = i, String = symb2->GetStringEntry(entry->_stringId) };
            }
        }

        protected override int OnCalculateSize(bool force)
        {
            _audioLen = 0;
            _headerLen = RWSDHeader.Size;
            foreach (ResourceNode g in Children)
                _headerLen += g.CalculateSize(true);
            foreach (WAVESoundNode s in Children[1].Children)
                _audioLen += s._audioSource.Length;

            return _headerLen + _audioLen;
        }
        protected internal override void OnRebuild(VoidPtr address, int length, bool force)
        {
            VoidPtr addr = address;

            RWSDHeader* header = (RWSDHeader*)address;
            header->_header._length = length;
            header->_header._tag = RWSDHeader.Tag;
            header->_header._numEntries = 2;
            header->_header._firstOffset = 0x20;
            header->_header._endian = -2;
            header->_header._version = 0x102;
            header->_dataOffset = 0x20;
            header->_dataLength = Children[0]._calcSize;
            header->_waveOffset = 0x20 + Children[0]._calcSize;
            header->_waveLength = Children[1]._calcSize;

            addr += 0x20; //Advance address to data header

            if (RSARNode == null)
            {
                VoidPtr audioAddr = addr;
                foreach (ResourceNode e in Children)
                    audioAddr += e._calcSize;
                (Children[1] as RWSDSoundGroupNode)._audioAddr = audioAddr;
            }
            else (Children[1] as RWSDSoundGroupNode)._audioAddr = _rebuildAudioAddr;

            Children[0].Rebuild(addr, Children[0]._calcSize, true);
            addr += Children[0]._calcSize;
            Children[1].Rebuild(addr, Children[1]._calcSize, true);
            addr += Children[1]._calcSize;
        }

        public override void Remove()
        {
            if (RSARNode != null)
                RSARNode.Files.Remove(this);
            base.Remove();
        }

        internal static ResourceNode TryParse(DataSource source) { return ((RWSDHeader*)source.Address)->_header._tag == RWSDHeader.Tag ? new RWSDNode() : null; }
    }
}
