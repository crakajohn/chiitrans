﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Globalization;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;

namespace ChiiTrans
{
    public partial class Form1 : Form
    {
        public static Form1 thisForm;
        public FormOptions formOptions;
        private FormAddReplacement formAddReplacement;

        public Form1()
        {
            InitializeComponent();
            thisForm = this;
            BackColor = Color.FromArgb(0, 0, 1);
            UpdateButtonOnOffDelegate = new UpdateButtonOnOffType(UpdateButtonOnOff);
            Global.Init();
            Global.runScript = RunScript;
            WindowPosition.Deserialize(this, Global.windowPosition.MainFormPosition);
            buttonOnOff.Image = imageList1.Images["loading"];
            UpdateToolbarVisible();
            browser.ObjectForScripting = Global.script;
            browser.Url = new Uri("file://" + Path.Combine(Application.StartupPath, "html\\base.html"));
            ApplyOptions();
            Global.agth.LoadAppProfiles();
            UseCommandLineArgs();
            if (Global.agth.TryConnectPipe())
                turnOn();
            else
                turnOff();
            buttonOnOff.Enabled = true;
            FormTooltip.instance.Show();
            this.Activate();

            Application.Idle += PreloadOnIdle;
        }

        private void PreloadThreadProc()
        {
            var ed = Edict.instance;
            var inflect = Inflect.instance;
            foreach (var trans in Global.options.translators)
            {
                if (trans.inUse)
                {
                    string name = Translation.Translators[trans.id];
                    if (name == "Atlas")
                        Atlas.Ready();
                    else if (name.IndexOf("MeCab") >= 0)
                        Mecab.Ready();
                }
            }
        }

        private void UseCommandLineArgs()
        {
            string[] args = Environment.GetCommandLineArgs();
            string keys = "";
            string app = null;
            foreach (string arg in args)
            {
                if (arg == args[0])
                    continue;
                if (arg.Length > 0)
                {
                    if (arg[0] == '/')
                    {
                        keys += arg + ' ';
                    }
                    else
                    {
                        if (app == null)
                            app = arg;
                    }
                }
            }
            if (app != null)
            {
                try
                {
                    Global.RunGame(app, keys);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        public static void Debug(string s)
        {
            if (thisForm.InvokeRequired)
            {
                ShowDebugDelegate tmp = new ShowDebugDelegate(thisForm.ShowDebug);
                thisForm.Invoke(tmp, s);
            }
            else
            {
                thisForm.ShowDebug(s);
            }
        }

        private delegate void ShowDebugDelegate(string s);
        private void ShowDebug(string s)
        {
            textBoxDebug.Visible = true;
            textBoxDebug.Text = s;
        }

        private void ApplyOptions()
        {
            this.TopMost = Global.isTopMost();
        }

        private void toggleOnOff()
        {
            if (Global.agth.is_on)
            {
                turnOff();
            }
            else
            {
                turnOn();
            }
        }

        private void turnOn()
        {
            if (!Global.agth.TurnOn())
                MessageBox.Show("AGTH or another instance of this program is running.\r\nClose AGTH and try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void turnOff()
        {
            Global.agth.TurnOff();
        }

        private delegate void UpdateButtonOnOffType(bool isOn);
        private UpdateButtonOnOffType UpdateButtonOnOffDelegate;
        public void UpdateButtonOnOff(bool isOn)
        {
            if (InvokeRequired)
            {
                Invoke(UpdateButtonOnOffDelegate, isOn);
            }
            else
            {
                if (isOn)
                {
                    buttonOnOff.Image = imageList1.Images["on"];
                    buttonOnOff.Text = "Turn off (T)";
                }
                else
                {
                    buttonOnOff.Image = imageList1.Images["off"];
                    buttonOnOff.Text = "Turn on (T)";
                }
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            toggleOnOff();
        }

        public delegate object RunScriptDelegate(string name, object[] args);

        public object RunScript(string name, object[] args)
        {
            try
            {
                if (InvokeRequired)
                {
                    RunScriptDelegate tmp = new RunScriptDelegate(RunScript);
                    return Invoke(tmp, name, args);
                }
                else
                {
                    return browser.Document.InvokeScript(name, args);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (formOptions == null)
                formOptions = new FormOptions();
            this.TopMost = false;
            if (formOptions.ShowDialog() == DialogResult.OK)
            {
                formOptions.SaveOptions();
                this.ApplyOptions();
                Global.script.UpdateBrowserSettings();
            }
            else
            {
                this.TopMost = Global.isTopMost();
            }
        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            Global.RunScript("ClearContent");
        }

        private void textBoxDebug_Click(object sender, EventArgs e)
        {
            textBoxDebug.SelectAll();
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (Global.fullscreen)
                Global.FullscreenOff();
            Global.windowPosition.MainFormPosition = WindowPosition.Serialize(this);
            if (FormMonitor.isCreated())
            {
                Global.windowPosition.MonitorFormPosition = WindowPosition.Serialize(FormMonitor.instance);
            }
            if (formOptions != null)
            {
                Global.windowPosition.OptionsFormPosition = WindowPosition.Serialize(formOptions);
            }
            if (HivemindSubmit.isCreated())
            {
                Global.windowPosition.HivemindSubmitFormPosition = WindowPosition.Serialize(HivemindSubmit.instance);
            }
            Global.windowPosition.Save();
            Hide();
            if (FormMonitor.isCreated())
            {
                FormMonitor.instance.Hide();
            }
            if (Global.script.transparentMode)
            {
                FormBottomLayer.instance.Hide();
            }
            if (Global.options.useCache)
                Global.cache.Save();
            Global.options.SaveOptions();
            Global.agth.Disconnect();
            Global.agth.SaveAppProfiles();
            if (Atlas.status == AtlasInitStatus.SUCCESS)
            {
                Atlas.Deinitialize();
            }
            if (Mecab.status == MecabInitStatus.SUCCESS)
            {
                Mecab.Deinitialize();
            }
            /*foreach (Translation trans in Translation.current)
            {
                trans.Abort();
            }*/
        }

        private string GetSelectedText()
        {
            object result = Global.RunScript("GetSelectedText");
            string selText = "";
            try
            {
                selText = (string)result;
                if (selText == null)
                    selText = "";
            }
            catch (Exception)
            { }
            return selText;
        }

        private string GetTextForAction()
        {
            string res = GetSelectedText();
            if (res == "")
                return Translation.lastGoodBuffer;
            else
                return res;
        }
        
        private void buttonAddReplacement_Click(object sender, EventArgs e)
        {
            string selText = GetSelectedText();
            if (formAddReplacement == null)
                formAddReplacement = new FormAddReplacement();
            string oldNewText = "";
            foreach (Replacement rep in Global.options.replacements)
            {
                if (rep.oldText == selText)
                {
                    oldNewText = rep.newText;
                    break;
                }
            }
            formAddReplacement.UpdateControls(selText, oldNewText);
            TopMost = false;
            if (formAddReplacement.ShowDialog() == DialogResult.OK)
            {
                string oldText, newText;
                formAddReplacement.GetControlValues(out oldText, out newText);
                Global.options.replacements.Add(new Replacement(oldText, newText));
                Global.options.SaveReplacements();
            }
            TopMost = Global.isTopMost();
        }

        bool duplicateWorkaround = false;
        Keys oldKey;
        private void browser_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == oldKey && duplicateWorkaround)
            {
                duplicateWorkaround = false;
            }
            else
            {
                oldKey = e.KeyCode;
                duplicateWorkaround = true;
                if (!e.Control && !e.Alt)
                {
                    if (e.KeyCode == Keys.Insert)
                        buttonAddReplacement_Click(sender, null);
                    else if (e.KeyCode == Keys.T)
                        button2_Click(sender, null);
                    else if (e.KeyCode == Keys.Delete)
                        button2_Click_1(sender, null);
                    else if (e.KeyCode == Keys.Escape)
                        TransparentModeOff();
                    else if (e.KeyCode == Keys.Space)
                    {
                        buttonTranslateSelected_Click(sender, null);
                        duplicateWorkaround = false;
                    }
                    else if (e.KeyCode == Keys.Add && Global.script.transparentMode)
                        ChangeBottomLayerOpacity(1);
                    else if (e.KeyCode == Keys.Subtract && Global.script.transparentMode)
                        ChangeBottomLayerOpacity(-1);
                    else if (e.KeyCode == Keys.M)
                        buttonMonitor_Click(sender, null);
                    else if (e.KeyCode == Keys.G)
                        buttonRun_Click(sender, null);
                    else if (e.KeyCode == Keys.O)
                        button1_Click(sender, null);
                    else if (e.KeyCode == Keys.K)
                        buttonKataHira_Click(sender, null);
                    else if (e.KeyCode == Keys.A)
                        buttonTranslateFull_Click(sender, null);
                    else if (e.KeyCode == Keys.D)
                        buttonDictionary_Click(sender, null);
                }
                else if (e.Control && e.KeyCode == Keys.V || e.Shift && e.KeyCode == Keys.Insert)
                {
                    TranslateFromClipboard();
                }
                else if (e.Control && e.KeyCode == Keys.C || e.Control && e.KeyCode == Keys.Insert)
                {
                    string sel = GetSelectedText();
                    if (sel == "")
                    {
                        sel = GetTextForAction();
                        if (sel != null)
                            Clipboard.SetText(sel);
                    }
                }
            }

        }

        private void buttonTransparent_Click(object sender, EventArgs e)
        {
            if (Global.script.transparentMode)
                TransparentModeOff();
            else
                TransparentModeOn();
        }

        string oldFormText;
        private void TransparentModeOn()
        {
            Global.script.transparentMode = true;
            UpdateToolbarVisible();
            TransparencyKey = Color.FromArgb(0, 0, 1);
            oldFormText = Text;
            Text = "Press Escape to restore the window";
            Global.RunScript("TransparentModeOn");
            ChangeBottomLayerOpacity(0);
            FormBottomLayer.instance.UpdatePos();
            FormBottomLayer.instance.BackColor = Global.options.colors["back_tmode"].color;
            FormBottomLayer.instance.Show();
            TopMost = true;
        }

        private void TransparentModeOff()
        {
            if (Global.script.transparentMode)
            {
                Global.script.transparentMode = false;
                Global.RunScript("TransparentModeOff");
                Text = oldFormText;
                FormBorderStyle = FormBorderStyle.Sizable;
                TransparencyKey = Color.Empty;
                UpdateToolbarVisible();
                FormBottomLayer.instance.Hide();
                TopMost = Global.isTopMost();
            }
        }

        private void buttonMonitor_Click(object sender, EventArgs e)
        {
            if (FormMonitor.instance.Visible)
            {
                FormMonitor.instance.Hide();
            }
            else
            {
                FormMonitor.instance.Show();
            }
        }

        private void buttonTranslateSelected_Click(object sender, EventArgs e)
        {
            Translation.Translate(GetTextForAction(), null);
        }

        private void buttonRun_Click(object sender, EventArgs e)
        {
            TopMost = false;
            if (Global.agth.appProfiles["profiles"].dict.Count == 0)
            {
                if (openFileDialogRun.ShowDialog() == DialogResult.OK)
                {
                    FormRun.instance.SetExeName(openFileDialogRun.FileName);
                    if (FormRun.instance.ShowDialog() == DialogResult.OK)
                    {
                        FormRun.instance.Run();
                    }
                }
            }
            else
            {
                if (FormRun.instance.ShowDialog() == DialogResult.OK)
                {
                    FormRun.instance.Run();
                }
            }
            TopMost = Global.isTopMost();
        }

        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] data = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (data.Length > 0)
                {
                    FormRun.instance.SetExeName(data[0]);
                    if (FormRun.instance.ShowDialog() == DialogResult.OK)
                    {
                        FormRun.instance.Run();
                    }
                }
            }
        }

        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Move;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void Form1_LocationChanged(object sender, EventArgs e)
        {
            if (Global.script != null && Global.script.transparentMode)
            {
                if (!suspendBottomLayerUpdates)
                    FormBottomLayer.instance.UpdatePos();
            }
        }

        private void Form1_SizeChanged(object sender, EventArgs e)
        {
            if (Global.script != null && Global.script.transparentMode)
            {
                browser.Size = ClientSize;
                if (!suspendBottomLayerUpdates)
                    FormBottomLayer.instance.UpdatePos();
            }
        }

        private void ChangeBottomLayerOpacity(int delta)
        {
            int value = Global.options.bottomLayerOpacity;
            value += delta * 10;
            if (value < 0) value = 0;
            if (value > 100) value = 100;
            Global.options.bottomLayerOpacity = value;
            FormBottomLayer.instance.Opacity = (double)value / 100;
        }

        public bool suspendBottomLayerUpdates = false;
        private void Form1_Activated(object sender, EventArgs e)
        {
            if (!suspendBottomLayerUpdates && Global.script.transparentMode && FormBorderStyle == FormBorderStyle.None)
            {
                suspendBottomLayerUpdates = true;
                //SuspendLayout();
                //browser.Hide();
                int oldWidth = Width;
                int oldHeight = Height;
                FormBorderStyle = FormBorderStyle.Sizable;
                int border = (Width - oldWidth) / 2;
                Left -= border;
                Top -= (Height - oldHeight - border);
                //browser.Show();
                //ResumeLayout(false);
                TopMost = true;
                FormBottomLayer.instance.UpdatePos();
                suspendBottomLayerUpdates = false;
            }
        }

        private void Form1_Deactivate(object sender, EventArgs e)
        {
            if (!suspendBottomLayerUpdates && Global.script.transparentMode && ActiveForm != FormTooltip.instance)
            {
                suspendBottomLayerUpdates = true;
                //SuspendLayout();
                //browser.Hide();
                int oldWidth = Width;
                int oldHeight = Height;
                FormBorderStyle = FormBorderStyle.None;
                int border = (oldWidth - Width) / 2;
                Left += border;
                Top += (oldHeight - Height - border);
                //browser.Show();
                //ResumeLayout(false);
                TopMost = true;
                FormBottomLayer.instance.UpdatePos();
                suspendBottomLayerUpdates = false;
                /*if (Global.fullscreen && Global.gameWindow != IntPtr.Zero)
                {
                    PInvokeFunc.SetWindowPos(Global.gameWindow, FormBottomLayer.instance.Handle, 0, 0, 0, 0, 19);
                }*/
                //TopMost = true;
                //FormBottomLayer.instance.UpdatePos();
            }
        }

        private void buttonFullscreen_Click(object sender, EventArgs e)
        {
            Global.ToggleFullscreen();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            const int WM_KEYDOWN = 0x100;
            const int WM_SYSKEYDOWN = 0x104;

            if (msg.Msg == WM_KEYDOWN || msg.Msg == WM_SYSKEYDOWN)
            {
                if (keyData == (Keys.Alt | Keys.F))
                {
                    buttonFullscreen_Click(this, null);
                    return true;
                }
                else if (keyData == (Keys.Alt | Keys.Z))
                {
                    buttonTransparent_Click(this, null);
                    return true;
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void TranslateFromClipboard()
        {
            if (Clipboard.ContainsText(TextDataFormat.UnicodeText))
            {
                string buffer = Clipboard.GetText();
                Translation.Translate(buffer, null);
            }
        }

        private void buttonPaste_Click(object sender, EventArgs e)
        {
            TranslateFromClipboard();
        }

        private void toolStripHomePage_Click(object sender, EventArgs e)
        {
            if (hasDebugProc)
            {
                DebugProc();
            }
            else
            {
                if (MessageBox.Show("Go to ChiiTrans' home page?\r\nhttp://sites.google.com/site/chiitranslator/", "ChiiTrans v." + Application.ProductVersion, MessageBoxButtons.OKCancel) == DialogResult.OK)
                {
                    Process.Start("http://sites.google.com/site/chiitranslator/");
                }
            }
        }

        private void buttonClearCache_Click(object sender, EventArgs e)
        {
            Global.cache.Clear();
            Global.cache.Save();
        }

        private void buttonTranslateFull_Click(object sender, EventArgs e)
        {
            Options options = Global.options.Clone();
            options.displayOriginal = true;
            if (options.wordParseMethod != Options.PARSE_WWWJDIC)
                options.wordParseMethod = Options.PARSE_WWWJDIC;
            else
                options.wordParseMethod = Options.PARSE_BUILTIN;
            foreach (TranslatorRecord rec in options.translators)
            {
                if (rec.id != 8) //Hivemind off
                    rec.inUse = true;
            }
            Translation.Translate(GetTextForAction(), options);
        }

        private void buttonKataHira_Click(object sender, EventArgs e)
        {
            Translation.Translate(Translation.KatakanaToHiragana(GetTextForAction()), null);
        }

        private void contextMenu_Opening(object sender, CancelEventArgs e)
        {
            menuitemPaste.Enabled = Clipboard.ContainsText();
            menuitemOnOff.Checked = Global.agth.is_on;
            menuItemFullscreen.Checked = Global.fullscreen;
            menuItemTransparent.Checked = Global.script.transparentMode;
            showToolbarToolStripMenuItem.Enabled = !Global.script.transparentMode;
            showToolbarToolStripMenuItem.Checked = Global.options.toolbarVisible && !Global.script.transparentMode;
        }

        private void menuitemCopy_Click(object sender, EventArgs e)
        {
            string sel = GetTextForAction();
            if (sel != null)
                Clipboard.SetText(sel);
        }

        private void reloadUserDictionaryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Edict.instance.ReloadUserDictionary();
        }

        private void showToolbarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!Global.script.transparentMode)
                Global.options.toolbarVisible = !Global.options.toolbarVisible;
            UpdateToolbarVisible();
        }

        private void UpdateToolbarVisible()
        {
            bool isVisible = Global.options.toolbarVisible && !Global.script.transparentMode;
            toolStrip1.Visible = isVisible;
            if (!isVisible)
            {
                browser.Top = 0;
            }
            else
            {
                browser.Top = 28;
            }
            browser.Height = ClientSize.Height - browser.Top;
        }

        private void buttonDictionary_Click(object sender, EventArgs e)
        {
            string s = GetSelectedText();
            if (s != null)
                s = s.Trim();
            if (string.IsNullOrEmpty(s))
                return;
            if (!Edict.instance.Ready)
                return;
            string[] res = Edict.instance.DictionarySearch(s);
            if (res != null)
                Global.RunScript("DictionarySearchResults", Translation.NextTransId(), s, string.Join("\r", res));
        }

        private bool hasDebugProc = false;
        public void DebugProc()
        {
            HashSet<string> st = new HashSet<string>();
            foreach (EdictEntry e in Edict.instance.dict)
            {
                foreach (string s in e.pos)
                    st.Add(s);
            }
            HashSet<string> inf = new HashSet<string>();
            foreach (Inflect.Record rec in Inflect.instance.conj)
            {
                inf.Add(rec.pos);
            }
            inf.ExceptWith(st);
            Form1.Debug(string.Join(",", inf.ToArray()));
        }

        private void PreloadOnIdle(object sender, EventArgs e)
        {
            Application.Idle -= PreloadOnIdle;
            Thread preloadThread = new Thread(PreloadThreadProc);
            preloadThread.Priority = ThreadPriority.Lowest;
            preloadThread.Start();
        }
    }
}
