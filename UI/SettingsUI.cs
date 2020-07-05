﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Accord.Video.DirectShow;
using LiveSplit.VAS.Models;
using LiveSplit.VAS.VASL;

namespace LiveSplit.VAS.UI
{
    public partial class SettingsUI : AbstractUI
    {
        internal readonly Dictionary<string, CheckBox> BasicSettings;

        private string ProfilePath { get { return _Component.ProfilePath; } set { _Component.ProfilePath = value; } }
        private string VideoDevice { get { return _Component.VideoDevice; } set { _Component.VideoDevice = value; } }
        private string GameVersion { get { return _Component.GameVersion; } set { _Component.GameVersion = value; } }
        private IDictionary<string, bool> BasicSettingsState => _Component.BasicSettingsState;
        private IDictionary<string, dynamic> CustomSettingsState => _Component.CustomSettingsState;

        public SettingsUI(VASComponent component) : base(component)
        {
            InitializeComponent();

            UpdateCustomSettingsVisibility();

            BasicSettings = new Dictionary<string, CheckBox>
            {
                // Capitalized names for saving it in XML.
                ["Start"] = ckbStart,
                ["Split"] = ckbSplit,
                ["Reset"] = ckbReset,
            };

            _Component.ProfileChanged += (sender, gameProfile) => txtGameProfile.Text = ProfilePath;
        }

        override public void Rerender()
        {
            FillboxCaptureDevice();
        }

        override public void Derender()
        {
            boxCaptureDevice.Items.Clear();
        }

        override internal void InitVASLSettings(VASLSettings settings, bool scriptLoaded)
        {
            // sigh
            try
            {
                InitCustomSettings(settings, scriptLoaded);
                InitBasicSettings(settings);
            }
            catch (Exception e1)
            {
                try
                {
                    treeCustomSettings.Invoke((MethodInvoker)delegate
                    {
                        InitCustomSettings(settings, scriptLoaded);
                        InitBasicSettings(settings);
                    });
                }
                catch (Exception e2)
                {
                    Log.Logger.Error(e1, "Could not load VASL Settings.");
                    Log.Logger.Error(e2, "");
                }
            }
        }

        private void InitCustomSettings(VASLSettings settings, bool scriptLoaded)
        {
            treeCustomSettings.BeginUpdate();
            treeCustomSettings.Nodes.Clear();

            var flat = new Dictionary<string, DropDownTreeNode>();

            foreach (VASLSetting setting in settings.OrderedSettings)
            {
                var value = setting.Value;
                if (CustomSettingsState.ContainsKey(setting.Id))
                    value = CustomSettingsState[setting.Id];

                var node = new DropDownTreeNode(setting.Label)
                {
                    Tag = setting,
                    ToolTipText = setting.ToolTip
                };
                node.ComboBox.Text = value.ToString();
                setting.Value = value;

                if (setting.Parent == null)
                {
                    treeCustomSettings.Nodes.Add(node);
                }
                else if (flat.ContainsKey(setting.Parent))
                {
                    flat[setting.Parent].Nodes.Add(node);
                }

                flat.Add(setting.Id, node);
            }

            foreach (var item in flat)
            {
                if (!item.Value.Checked)
                {
                    UpdateGrayedOut(item.Value);
                }
            }

            treeCustomSettings.ExpandAll();
            treeCustomSettings.EndUpdate();

            // Scroll up to the top
            if (treeCustomSettings.Nodes.Count > 0)
                treeCustomSettings.Nodes[0].EnsureVisible();

            UpdateCustomSettingsVisibility();
        }

        internal bool FillboxCaptureDevice()
        {
            var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);

            if (videoDevices.Count > 0)
            {
                boxCaptureDevice.Enabled = true;

                int selectedIndex = boxCaptureDevice.SelectedIndex;
                boxCaptureDevice.Items.Clear();

                if (!string.IsNullOrEmpty(VideoDevice))
                {
                    var savedDevices = videoDevices.Where(d => d.ToString() == VideoDevice);
                    if (savedDevices.Count() > 0)
                    {
                        var savedDevice = savedDevices.First();
                        selectedIndex = videoDevices.IndexOf(savedDevice);
                    }
                }

                for (var i = 0; i < videoDevices.Count; i++)
                {
                    boxCaptureDevice.Items.Add(videoDevices[i]);
                }

                boxCaptureDevice.SelectedIndex = selectedIndex;
                return true;
            }
            else
            {
                boxCaptureDevice.Enabled = false;
                boxCaptureDevice.Items.Clear();
                return false;
            }
        }

        private void UpdateCustomSettingsVisibility()
        {
            bool show = treeCustomSettings.GetNodeCount(false) > 0;
            treeCustomSettings.Visible = show;
            btnResetToDefault.Visible  = show;
            btnCheckAll.Visible        = show;
            btnUncheckAll.Visible      = show;
            lblAdvanced.Visible        = show;
        }

        private void UpdateNodesInTree(Func<DropDownTreeNode, bool> func, TreeNodeCollection nodes)
        {
            foreach (DropDownTreeNode node in nodes)
            {
                bool include_child_nodes = func(node);
                if (include_child_nodes)
                    UpdateNodesInTree(func, node.Nodes);
            }
        }

        internal void UpdateNodesValues(Func<VASLSetting, dynamic> func, TreeNodeCollection nodes = null)
        {
            if (nodes == null)
                nodes = treeCustomSettings.Nodes;

            UpdateNodesInTree(node =>
            {
                var setting = (VASLSetting)node.Tag;
                dynamic value = func(setting);

                if (node.ComboBox.Text != value.ToString())
                    node.ComboBox.Text = value.ToString();

                return true;
            }, nodes);
        }

        internal void UpdateNodesValues(IDictionary<string, dynamic> settingValues, TreeNodeCollection nodes = null)
        {
            if (settingValues == null)
                return;

            UpdateNodesValues(setting =>
            {
                string id = setting.Id;

                if (settingValues.ContainsKey(id))
                    return settingValues[id];

                return setting.Value;
            }, nodes);
        }

        internal void UpdateNodeValue(Func<VASLSetting, dynamic> func, DropDownTreeNode node)
        {
            var setting = (VASLSetting)node.Tag;
            dynamic value = func(setting);

            if (node.ComboBox.Text != value.ToString())
                node.ComboBox.Text = value.ToString();
        }

        private void UpdateGrayedOut(DropDownTreeNode node)
        {
            if (node.ForeColor != SystemColors.GrayText)
            {
                UpdateNodesInTree(n =>
                {
                    n.ForeColor = node.Checked ? SystemColors.WindowText : SystemColors.GrayText;
                    return n.Checked || !node.Checked;
                }, node.Nodes);
            }
        }

        private void InitBasicSettings(VASLSettings settings)
        {
            foreach (var item in BasicSettings)
            {
                string name = item.Key.ToLower();
                CheckBox checkbox = item.Value;

                if (settings.IsBasicSettingPresent(name))
                {
                    VASLSetting setting = settings.BasicSettings[name];
                    checkbox.Enabled = true;
                    checkbox.Tag = setting;
                    var value = setting.Value;

                    if (BasicSettingsState.ContainsKey(name))
                        value = BasicSettingsState[name];

                    checkbox.Checked = value;
                    setting.Value = value;
                }
                else
                {
                    checkbox.Tag = null;
                    checkbox.Enabled = false;
                    checkbox.Checked = false;
                }
            }
        }

        // Events

        private void btnGameProfile_Click(object sender, EventArgs e)
        {
            if (ModifierKeys.HasFlag(Keys.Shift))
                SetGameProfileWithFolder();
            else
                SetGameProfileWithFile();
        }

        private void SetGameProfileWithFile()
        {
            using (var ofd = new OpenFileDialog()
            {
                Filter = "VAS files (*.vas)|*.vas|ZIP files|*.zip|All files (*.*)|*.*",
                Title = "Load a Game Profile"
            })
            {
                if (File.Exists(ProfilePath))
                {
                    ofd.InitialDirectory = Path.GetDirectoryName(ProfilePath);
                    ofd.FileName = Path.GetFileName(ProfilePath);
                }
                else if (Directory.Exists(ProfilePath))
                {
                    ofd.InitialDirectory = Path.GetDirectoryName(ProfilePath);
                }

                if (ofd.ShowDialog() == DialogResult.OK && ofd.CheckFileExists == true)
                {
                    TryLoadGameProfile(ofd.FileName);
                }
            }
        }

        private void SetGameProfileWithFolder()
        {
            using (var fbd = new FolderBrowserDialog() { ShowNewFolderButton = false })
            {
                if (Directory.Exists(ProfilePath))
                {
                    fbd.SelectedPath = ProfilePath;
                }
                else if (File.Exists(ProfilePath))
                {
                    fbd.SelectedPath = Path.GetDirectoryName(ProfilePath);
                }

                if (fbd.ShowDialog() == DialogResult.OK && Directory.Exists(fbd.SelectedPath))
                {
                    TryLoadGameProfile(fbd.SelectedPath);
                }
            }
        }

        private void TryLoadGameProfile(string filePath)
        {
            retry:
            var gp = GameProfile.FromPath(filePath);

            if (gp == null)
            {
                DialogResult dr = MessageBox.Show(
                    "Failed to load Game Profile.",
                    "Error",
                    MessageBoxButtons.RetryCancel,
                    MessageBoxIcon.Error
                    );

                if (dr == DialogResult.Retry)
                {
                    goto retry;
                }
            }
            else
            {
                ProfilePath = txtGameProfile.Text = filePath;
            }
        }

        // Basic Setting checked/unchecked
        //
        // This detects both changes made by the user and by the program, so this should
        // change the state in _basic_settings_state fine as well.
        private void methodCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            var checkbox = (CheckBox)sender;
            var setting = (VASLSetting)checkbox.Tag;

            if (setting != null)
            {
                BasicSettingsState[setting.Id] = setting.Value = checkbox.Checked;
            }
        }

        // Custom Setting checked/unchecked (only after initially building the tree)
        private void settingsTree_AfterCheck(object sender, TreeViewEventArgs e)
        {
            // Update value in the ASLSetting object, which also changes it in the ASL script
            var node = (DropDownTreeNode)e.Node;
            VASLSetting setting = (VASLSetting)node.Tag;
            setting.Value = node.Checked;
            CustomSettingsState[setting.Id] = setting.Value;

            UpdateGrayedOut(node);
        }

        private void settingsTree_BeforeCheck(object sender, TreeViewCancelEventArgs e)
        {
            e.Cancel = e.Node.ForeColor == SystemColors.GrayText;
        }

        // Custom Settings Button Events

        private void btnCheckAll_Click(object sender, EventArgs e)
        {
            UpdateNodesValues(s => true);
        }

        private void btnUncheckAll_Click(object sender, EventArgs e)
        {
            UpdateNodesValues(s => false);
        }

        private void btnResetToDefault_Click(object sender, EventArgs e)
        {
            UpdateNodesValues(s => s.DefaultValue);
        }


        // Custom Settings Context Menu Events

        private void settingsTree_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            // Select clicked node (not only with left-click) for use with context menu
            treeCustomSettings.SelectedNode = e.Node;
        }

        private void cmiCheckBranch_Click(object sender, EventArgs e)
        {
            UpdateNodesValues(s => true, treeCustomSettings.SelectedNode.Nodes);
            UpdateNodeValue(s => true, (DropDownTreeNode)treeCustomSettings.SelectedNode);
        }

        private void cmiUncheckBranch_Click(object sender, EventArgs e)
        {
            UpdateNodesValues(s => false, treeCustomSettings.SelectedNode.Nodes);
            UpdateNodeValue(s => false, (DropDownTreeNode)treeCustomSettings.SelectedNode);
        }

        private void cmiResetBranchToDefault_Click(object sender, EventArgs e)
        {
            UpdateNodesValues(s => s.DefaultValue, treeCustomSettings.SelectedNode.Nodes);
            UpdateNodeValue(s => s.DefaultValue, (DropDownTreeNode)treeCustomSettings.SelectedNode);
        }

        private void cmiExpandBranch_Click(object sender, EventArgs e)
        {
            treeCustomSettings.SelectedNode.ExpandAll();
            treeCustomSettings.SelectedNode.EnsureVisible();
        }

        private void cmiCollapseBranch_Click(object sender, EventArgs e)
        {
            treeCustomSettings.SelectedNode.Collapse();
            treeCustomSettings.SelectedNode.EnsureVisible();
        }

        private void cmiCollapseTreeToSelection_Click(object sender, EventArgs e)
        {
            TreeNode selected = treeCustomSettings.SelectedNode;
            treeCustomSettings.CollapseAll();
            treeCustomSettings.SelectedNode = selected;
            selected.EnsureVisible();
        }

        private void cmiExpandTree_Click(object sender, EventArgs e)
        {
            treeCustomSettings.ExpandAll();
            treeCustomSettings.SelectedNode.EnsureVisible();
        }

        private void cmiCollapseTree_Click(object sender, EventArgs e)
        {
            treeCustomSettings.CollapseAll();
        }

        private void cmiResetSettingToDefault_Click(object sender, EventArgs e)
        {
            UpdateNodeValue(s => s.DefaultValue, (DropDownTreeNode)treeCustomSettings.SelectedNode);
        }

        private void btnCaptureDevice_Click(object sender, EventArgs e)
        {
            retry:
            var success = FillboxCaptureDevice();
            if (!success)
            {
                DialogResult dr = MessageBox.Show(
                    "No video capture devices detected.",
                    "Information",
                    MessageBoxButtons.RetryCancel,
                    MessageBoxIcon.Information
                    );

                if (dr == DialogResult.Retry)
                {
                    goto retry;
                }
            }
        }

        private void boxCaptureDevice_SelectedIndexChanged(object sender, EventArgs e)
        {
            // May need to tweak more
            if (boxCaptureDevice.SelectedItem.ToString() == VideoDevice)
                return;

            if (_Component.Scanner.IsVideoSourceRunning())
                _Component.Scanner.Stop();
            retry:
            var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            var matches = videoDevices.Where(v => v.ToString() == boxCaptureDevice.Text);
            if (matches.Count() > 0)
            {
                var match = matches.First();
                VideoDevice = match.ToString();
                _Component.Scanner.AsyncStart();
            }
            else
            {
                lblCaptureDevice.Text = "Capture Device";
                DialogResult dr = MessageBox.Show(
                    "Selected video capture device cannont be found. Has it been unplugged?",
                    "Error",
                    MessageBoxButtons.RetryCancel,
                    MessageBoxIcon.Error
                    );

                if (dr == DialogResult.Retry)
                {
                    goto retry;
                }
                else
                {
                    FillboxCaptureDevice();
                }
            }
        }

        private void SettingsUI_VisibleChanged(object sender, EventArgs e)
        {
            Rerender();
        }
    }
}
