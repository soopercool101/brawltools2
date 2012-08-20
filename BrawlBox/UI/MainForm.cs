﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Imaging;
using System.Reflection;
using BrawlLib.IO;
using System.Audio;
using BrawlLib.Wii.Audio;

namespace BrawlBox
{
    public partial class MainForm : Form
    {
        private static MainForm _instance;
        public static MainForm Instance { get { return _instance == null ? _instance = new MainForm() : _instance; } }

        private BaseWrapper _root;
        public BaseWrapper RootNode { get { return _root; } }

        private SettingsDialog _settings;
        private SettingsDialog Settings { get { return _settings == null ? _settings = new SettingsDialog() : _settings; } }

        public MainForm()
        {
            InitializeComponent();
            this.Text = Program.AssemblyTitle;
            soundPackControl1.lstSets.SmallImageList = ResourceTree.Images;
            previewPanel1.Dock = 
            msBinEditor1.Dock = 
            animEditControl.Dock = 
            texAnimEditControl.Dock = 
            shpAnimEditControl.Dock = 
            soundPackControl1.Dock = 
            audioPlaybackPanel1.Dock = 
            clrControl.Dock = 
            visEditor.Dock = 
            offsetEditor1.Dock = 
            attributeControl.Dock = 
            articleAttributeGrid.Dock = 
            movesetEditor1.Dock = DockStyle.Fill;
            m_DelegateOpenFile = new DelegateOpenFile(Program.Open);
        }

        private delegate bool DelegateOpenFile(String s);
        private DelegateOpenFile m_DelegateOpenFile;

        public void Reset()
        {
            _root = null;
            resourceTree.SelectedNode = null;
            resourceTree.Clear();

            if (Program.RootNode != null)
            {
                _root = BaseWrapper.Wrap(this, Program.RootNode);
                resourceTree.BeginUpdate();
                resourceTree.Nodes.Add(_root);
                resourceTree.SelectedNode = _root;
                _root.Expand();
                resourceTree.EndUpdate();

                closeToolStripMenuItem.Enabled = true;
                saveAsToolStripMenuItem.Enabled = true;
                saveToolStripMenuItem.Enabled = true;
            }
            else
            {
                closeToolStripMenuItem.Enabled = false;
                saveAsToolStripMenuItem.Enabled = false;
                saveToolStripMenuItem.Enabled = false;
            }

            UpdateName();
        }

        public void UpdateName()
        {
            if (Program.RootPath != null)
                this.Text = String.Format("{0} - {1}", Program.AssemblyTitle, Program.RootPath);
            else
                this.Text = Program.AssemblyTitle;
        }

        public void TargetResource(ResourceNode n)
        {
            if (_root != null)
                resourceTree.SelectedNode = _root.FindResource(n, true);
        }

        private Control _currentControl = null;
        private Control _secondaryControl = null;
        private void resourceTree_SelectionChanged(object sender, EventArgs e)
        {
            Image img = previewPanel1.Picture;
            if (img != null)
            {
                previewPanel1.Picture = null;
                img.Dispose();
            }

            audioPlaybackPanel1.TargetSource = null;
            articleAttributeGrid.TargetNode = null;
            animEditControl.TargetSequence = null;
            texAnimEditControl.TargetSequence = null;
            shpAnimEditControl.TargetSequence = null;
            msBinEditor1.CurrentNode = null;
            soundPackControl1.TargetNode = null;
            clrControl.ColorSource = null;
            visEditor.TargetNode = null;
            movesetEditor1.TargetNode = null;
            attributeControl.TargetNode = null;
            offsetEditor1.TargetNode = null;

            Control newControl = null;
            Control newControl2 = null;

            BaseWrapper w;
            ResourceNode node;
            if ((resourceTree.SelectedNode is BaseWrapper) && ((node = (w = resourceTree.SelectedNode as BaseWrapper).ResourceNode) != null))
            {
                propertyGrid1.SelectedObject = node;

                if (node is IImageSource)
                {
                    previewPanel1.Picture = ((IImageSource)node).GetImage(0);
                    newControl = previewPanel1;
                }
                else if (node is MSBinNode)
                {
                    msBinEditor1.CurrentNode = node as MSBinNode;
                    newControl = msBinEditor1;
                }
                else if (node is CHR0EntryNode)
                {
                    animEditControl.TargetSequence = node as CHR0EntryNode;
                    newControl = animEditControl;
                }
                else if (node is SRT0TextureNode)
                {
                    texAnimEditControl.TargetSequence = node as SRT0TextureNode;
                    newControl = texAnimEditControl;
                }
                else if (node is SHP0VertexSetNode)
                {
                    shpAnimEditControl.TargetSequence = node as SHP0VertexSetNode;
                    newControl = shpAnimEditControl;
                }
                else if (node is RSARNode)
                {
                    soundPackControl1.TargetNode = node as RSARNode;
                    newControl = soundPackControl1;
                }
                else if (node is IAudioSource)
                {
                    audioPlaybackPanel1.TargetSource = node as IAudioSource;
                    newControl = audioPlaybackPanel1;
                }
                else if (node is VIS0EntryNode)
                {
                    visEditor.TargetNode = node as VIS0EntryNode;
                    newControl = visEditor;
                }
                else if (node is MoveDefActionNode)
                {
                    movesetEditor1.TargetNode = node as MoveDefActionNode;
                    newControl = movesetEditor1;
                }
                else if (node is MoveDefEventOffsetNode)
                {
                    offsetEditor1.TargetNode = node as MoveDefEventOffsetNode;
                    newControl = offsetEditor1;
                }
                else if (node is MoveDefEventNode)
                {
                    //if (node.Parent is MoveDefLookupEntry1Node)
                    //    eventDescription1.SetTarget((node as MoveDefLookupEntry1Node).EventInfo, -1);
                    //else
                        eventDescription1.SetTarget((node as MoveDefEventNode).EventInfo, -1);
                    newControl = eventDescription1;
                }
                else if (node is MoveDefEventParameterNode)
                {
                    //if (node.Parent is MoveDefLookupEntry1Node)
                    //    eventDescription1.SetTarget((node.Parent as MoveDefLookupEntry1Node).EventInfo, node.Index == -1 ? -2 : node.Index);
                    //else
                        eventDescription1.SetTarget((node.Parent as MoveDefEventNode).EventInfo, node.Index == -1 ? -2 : node.Index);
                    newControl = eventDescription1;
                }
                else if (node is MoveDefAttributeNode)
                {
                    attributeControl.TargetNode = node as MoveDefAttributeNode;
                    newControl = attributeControl;
                }
                else if (node is MoveDefSectionParamNode)
                {
                    articleAttributeGrid.TargetNode = node as MoveDefSectionParamNode;
                    newControl = articleAttributeGrid;
                }
                if (node is IColorSource)
                {
                    clrControl.ColorSource = node as IColorSource;
                    if (((IColorSource)node).ColorCount > 0)
                    if (newControl != null)
                        newControl2 = clrControl;
                    else
                        newControl = clrControl;
                }
                else if (node is RELSectionNode)
                {
                    if (((RELSectionNode)node).CodeSection)
                    {
                        relDisassembler1.Section = (RELSectionNode)node;
                        newControl = relDisassembler1;
                    }
                }

                if ((editToolStripMenuItem.DropDown = w.ContextMenuStrip) != null)
                    editToolStripMenuItem.Enabled = true;
                else
                    editToolStripMenuItem.Enabled = false;
            }
            else
            {
                propertyGrid1.SelectedObject = null;

                editToolStripMenuItem.DropDown = null;
                editToolStripMenuItem.Enabled = false;
            }

            if (_secondaryControl != newControl2)
            {
                if (_secondaryControl != null)
                {
                    _secondaryControl.Dock = DockStyle.Fill;
                    _secondaryControl.Visible = false;
                }
                _secondaryControl = newControl2;
                if (_secondaryControl != null)
                {
                    _secondaryControl.Dock = DockStyle.Right;
                    _secondaryControl.Visible = true;
                    _secondaryControl.Width = 340;
                }
            }
            if (_currentControl != newControl)
            {
                if (_currentControl != null)
                    _currentControl.Visible = false;
                _currentControl = newControl;
                if (_currentControl != null)
                    _currentControl.Visible = true;
            }
            if (_currentControl != null)
            if (_secondaryControl != null)
            {
                _currentControl.Dock = DockStyle.Left;
                _currentControl.Width = splitContainer2.Panel2.Width - _secondaryControl.Width;
            }
            else
                _currentControl.Dock = DockStyle.Fill;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!Program.Close()) e.Cancel = true;
            base.OnClosing(e);
        }

        private static string _inFilter =
        "All Supported Formats |*.pac;*.pcs;*.brres;*.brtex;*.brmdl;*.breff;*.breft;*.plt0;*.tex0;*.mdl0;*.chr0;*.srt0;*.shp0;*.pat0;*.vis0;*.clr0;*.brstm;*.brsar;*.msbin;*.rwsd;*.rseq;*.rbnk;*.efls;*.breff;*.breft;*.arc;*.rel;*.szs;*.mrg;*.mrgc|" +
        "PAC File Archive (*.pac)|*.pac|" +
        "PCS Compressed File Archive (*.pcs)|*.pcs|" +
        "Resource Package (*.brres;*.brtex;*.brmdl)|*.brres;*.brtex;*.brmdl|" +
        "PLT0 Palette (*.plt0)|*.plt0|" +
        "TEX0 Texture (*.tex0)|*.tex0|" +
        "MDL0 Model (*.mdl0)|*.mdl0|" +
        "CHR0 Model Animation (*.chr0)|*.chr0|" +
        "SRT0 Texture Animation (*.srt0)|*.srt0|" +
        "SHP0 Vertex Morph (*.shp0)|*.shp0|" +
        "PAT0 Texture Pattern (*.pat0)|*.pat0|" +
        "VIS0 Visibility Sequence (*.vis0)|*.vis0|" +
        "Color Sequence (*.clr0)|*.clr0|" +
        "BRSTM Audio Stream (*.brstm)|*.brstm|" +
        "BRSAR Audio Package (*.brsar)|*.brsar|" +
        "MSBin Message Pack (*.msbin)|*.msbin|" +
        "Raw Sound Pack (*.rwsd)|*.rwsd|" +
        "Raw Sound Sequence (*.rseq)|*.rseq|" +
        "Raw Sound Bank (*.rbnk)|*.rbnk|" +
        "EFLS Effect List (*.efls)|*.efls|" +
        "REFF Effect Controls (*.breff)|*.breff|" +
        "REFT Effect Textures (*.breft)|*.breft|" +
        "ARC File Archive (*.arc)|*.arc|" +
        "REL Module (*.rel)|*.rel|" +
        "DOL Static Module (*.dol)|*.dol|" +
        "SZS File Archive (*.szs)|*.szs|" +
        "MRG Resource Group (*.mrg;*.mrgc)|*.mrg;*.mrgc";

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string inFile;
            if (Program.OpenFile(_inFilter, out inFile) != 0)
                Program.Open(inFile);
        }

        #region File Menu
        private void aRCArchiveToolStripMenuItem_Click(object sender, EventArgs e) { Program.New<ARCNode>(); }
        private void brresPackToolStripMenuItem_Click(object sender, EventArgs e) { Program.New<BRESNode>(); }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e) { Program.Save(); }
        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e) { Program.SaveAs(); }
        private void closeToolStripMenuItem_Click(object sender, EventArgs e) { Program.Close(); }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e) { this.Close(); }
        #endregion

        private void fileResizerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //using (FileResizer res = new FileResizer())
            //    res.ShowDialog();
        }
        private void settingsToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            Settings.ShowDialog();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e) { AboutForm.Instance.ShowDialog(this); }

        private void bRStmAudioToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string path;
            if (Program.OpenFile("PCM Audio (*.wav)|*.wav", out path) > 0)
            {
                if (Program.New<RSTMNode>())
                {
                    using (BrstmConverterDialog dlg = new BrstmConverterDialog())
                    {
                        dlg.AudioSource = path;
                        if (dlg.ShowDialog(this) == DialogResult.OK)
                        {
                            Program.RootNode.Name = Path.GetFileNameWithoutExtension(dlg.AudioSource);
                            Program.RootNode.ReplaceRaw(dlg.AudioData);
                        }
                        else
                            Program.Close(true);
                    }
                }
            }
        }

        private void propertyGrid1_SelectedGridItemChanged(object sender, SelectedGridItemChangedEventArgs e)
        {
            //object o;
            //GoodColorDialog d;
            //if ((o = propertyGrid1.SelectedObject) is ResourceNode)
            //{
            //    ResourceNode n = (ResourceNode)o;
            //    if ((o = propertyGrid1.SelectedGridItem.Value) is RGBAPixel)
            //    {
            //        RGBAPixel p = (RGBAPixel)o;
            //        if ((d = new GoodColorDialog() { Color = Color.FromArgb(p.A, p.R, p.G, p.B) }).ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
            //        {

            //        }
            //    }
            //}
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            Array a = (Array)e.Data.GetData(DataFormats.FileDrop);
            if (a != null)
            {
                string s = null;
                for (int i = 0; i < a.Length; i++)
                {
                    s = a.GetValue(i).ToString();
                    this.BeginInvoke(m_DelegateOpenFile, new Object[] { s });
                }
            }
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }
    }
}
