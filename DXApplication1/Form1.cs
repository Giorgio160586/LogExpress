using DevExpress.Mvvm.Native;
using DevExpress.XtraBars;
using DevExpress.XtraBars.Ribbon;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraRichEdit;
using DevExpress.XtraRichEdit.API.Native;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace LogExpress_NET8
{
    public partial class Form1 : RibbonForm
    {
        private const string subkey = @"SOFTWARE\LogExpress";

        private Color[] colors = {
            Color.FromArgb(255, 0, 209, 246),
            Color.FromArgb(255, 83, 186, 122),
            Color.FromArgb(255, 252, 109, 119) };

        private List<string> find1Filter { get; set; }
        private List<string> find2Filter { get; set; }
        private List<string> find3Filter { get; set; }

        private List<string> textHistory { get; set; }
        private int currentIndex;
        public static string version
        {
            get
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
                return $"{versionInfo.FileVersion} (BETA) -";
            }
        }

        private int searchStartIndex = 0;

        private bool formLoaded = false;
        public Form1()
        {
            InitializeComponent();

            textHistory = new List<string>();

            ResizeFormToScreenPercentage(0.75);

            var process = Environment.Is64BitProcess ? "x64" : "x86";
            Text += $" - v{version} ({process})";

            splitContainerControl1.SplitterPosition = splitContainerControl1.Height / 12 * 4;

            repositoryItemCheckedComboBoxEdit1.Appearance.ForeColor = colors[0];
            repositoryItemCheckedComboBoxEdit2.Appearance.ForeColor = colors[1];
            repositoryItemCheckedComboBoxEdit3.Appearance.ForeColor = colors[2];

            barStaticItem2.Appearance.ForeColor = colors[0];
            barStaticItem3.Appearance.ForeColor = colors[1];
            barStaticItem4.Appearance.ForeColor = colors[2];
        }
        private void Form1_Activated(object sender, EventArgs e)
        {
            if (!formLoaded)
            {
                Application.DoEvents();
                formLoaded = true;

                LoadFromRegistry($@"{subkey}\FindAll1", Find1BarEditItem);
                LoadFromRegistry($@"{subkey}\FindAll2", Find2BarEditItem);
                LoadFromRegistry($@"{subkey}\FindAll3", Find3BarEditItem);


                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(subkey))
                {
                    if (key != null)
                    {
                        SetText(Convert.ToString(key.GetValue("Text")));
                    }
                }
            }
        }
        private void LoadFromRegistry(string registryPath, BarEditItem barEditItem)
        {
            var comboBox = ((RepositoryItemCheckedComboBoxEdit)barEditItem.Edit);
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registryPath))
            {
                if (key != null)
                {
                    comboBox.Items.Clear();
                    foreach (string subKeyName in key.GetSubKeyNames())
                    {
                        using (RegistryKey itemKey = key.OpenSubKey(subKeyName))
                        {
                            if (itemKey != null)
                            {
                                string itemValue = (string)itemKey.GetValue("Item", string.Empty);
                                bool isChecked = (string)itemKey.GetValue("Checked", "False") == "True";

                                if (!string.IsNullOrEmpty(itemValue))
                                    comboBox.Items.Add(itemValue, isChecked);
                            }
                        }
                    }
                }
            }
            barEditItem.EditValue = comboBox.GetCheckedItems();
        }
        private void ResizeFormToScreenPercentage(double percentage)
        {
            Rectangle screenBounds = Screen.PrimaryScreen.Bounds;
            int newWidth = (int)(screenBounds.Width * percentage);
            int newHeight = (int)(screenBounds.Height * percentage);
            Width = newWidth;
            Height = newHeight;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(
                (screenBounds.Width - Width) / 2,
                (screenBounds.Height - Height) / 2
            );
        }
        private void FindBarButtonItem_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            DevExpress.XtraSplashScreen.SplashScreenManager.ShowForm(this, typeof(DXWaitForm), true, true, false);

            var text = FromRichEdit.Text;

            AddItemToRepository(Convert.ToString(Find1BarEditItem.EditValue), repositoryItemCheckedComboBoxEdit1);
            AddItemToRepository(Convert.ToString(Find2BarEditItem.EditValue), repositoryItemCheckedComboBoxEdit2);
            AddItemToRepository(Convert.ToString(Find3BarEditItem.EditValue), repositoryItemCheckedComboBoxEdit3);

            SaveReg();

            var l = text
                .Split('\n')
                .Where(f => !string.IsNullOrEmpty(f))
                .ToArray();

            find1Filter = Convert.ToString(Find1BarEditItem.EditValue).Split(',')
                .Where(f => !string.IsNullOrEmpty(f.Trim())).Select(s => s.Trim()).ToList();
            find2Filter = Convert.ToString(Find2BarEditItem.EditValue).Split(',')
                .Where(f => !string.IsNullOrEmpty(f.Trim())).Select(s => s.Trim()).ToList();
            find3Filter = Convert.ToString(Find3BarEditItem.EditValue).Split(',')
                .Where(f => !string.IsNullOrEmpty(f.Trim())).Select(s => s.Trim()).ToList();

            var filters = find1Filter.Concat(find2Filter).Concat(find3Filter).ToList();

            if (filters.Count > 0)
                l = l.Where(f => filters.Any(filter => f.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();

            UpdateBarCaption(barStaticItem2, find1Filter, l);
            UpdateBarCaption(barStaticItem3, find2Filter, l);
            UpdateBarCaption(barStaticItem4, find3Filter, l);

            l = FilterListByCheckItem(l, Contains1BarCheckItem, find1Filter);
            l = FilterListByCheckItem(l, Contains2BarCheckItem, find2Filter);
            l = FilterListByCheckItem(l, Contains3BarCheckItem, find3Filter);

            ToRichEdit.Text = string.Join("\n", l.Distinct());

            CustomHighlightText(FromRichEdit);
            CustomHighlightText(ToRichEdit);

            ClearMemory();
            DevExpress.XtraSplashScreen.SplashScreenManager.CloseForm(false);
        }
        private void AddItemToRepository(string filter, RepositoryItemCheckedComboBoxEdit repository)
        {
            var list = filter.Split(',').Select(s => s.Trim());
            foreach (var item in list)
            {
                if (!string.IsNullOrEmpty(item) && !repository.Items.Contains(item))
                    repository.Items.Add(item, true);

            }
        }
        private void UpdateBarCaption(BarStaticItem barItem, List<string> findTerms, string[] lines)
        {
            if (findTerms != null && findTerms.Any())
            {
                barItem.Caption = string.Join(", ",
                    findTerms.Select(term =>
                    {
                        int hitCount = lines.Count(f => f.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
                        term = term.Length > 20 ? term.Substring(0, 17) + "..." : term;
                        return $"{term} ({hitCount} hits)";
                    }));
            }
            else
            {
                barItem.Caption = string.Empty;
            }
        }
        private string[] FilterListByCheckItem(string[] list, BarCheckItem checkItem, List<string> terms)
        {
            if (checkItem.Checked && terms != null && terms.Any())
                return list.Where(f => terms.Any(term => f.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
            return list;
        }
        private void CustomHighlightText(RichEditControl richEditControl)
        {
            ClearHighlight(richEditControl);
            List<string>[] findTerms = { find1Filter, find2Filter, find3Filter };

            for (int i = 0; i < findTerms.Length; i++)
            {
                if (findTerms[i] != null && findTerms[i].Any())
                {
                    foreach (var term in findTerms[i])
                    {
                        HighlightWord(richEditControl, term, colors[i]);
                    }
                }
            }
        }
        private void HighlightWord(RichEditControl richEditControl, string word, Color color)
        {
            var document = richEditControl.Document;
            document.BeginUpdate();
            try
            {
                SearchOptions searchOptions = SearchOptions.None;
                DocumentRange[] foundRanges = document.FindAll(word, searchOptions);
                foreach (DocumentRange range in foundRanges)
                {
                    CharacterProperties cp = document.BeginUpdateCharacters(range);
                    cp.ForeColor = color;
                    document.EndUpdateCharacters(cp);
                }
            }
            finally
            {
                document.EndUpdate();
            }
        }

        private void ClearHighlight(RichEditControl richEditControl)
        {
            var document = richEditControl.Document;
            document.BeginUpdate();
            try
            {
                CharacterProperties defaultProps = document.BeginUpdateCharacters(document.Range);
                defaultProps.ForeColor = richEditControl.Appearance.Text.ForeColor;
                document.EndUpdateCharacters(defaultProps);
            }
            finally
            {
                document.EndUpdate();
            }
        }

        private void SetText(string text)
        {
            DevExpress.XtraSplashScreen.SplashScreenManager.ShowForm(this, typeof(DXWaitForm), true, true, false);
            FromRichEdit.Text = text;
            AddTextToHistory(text);
            DevExpress.XtraSplashScreen.SplashScreenManager.CloseForm(false);
        }
        private void FromRichEdit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.V)
            {
                SetText(Clipboard.GetText());
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
            if (e.Control && e.KeyCode == Keys.Z)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                RestorePreviousText();
            }
            else if (e.Control && e.KeyCode == Keys.Y)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                RestoreNextText();
            }
        }
        private void ToRichEdit_DoubleClick(object sender, EventArgs e)
        {
            DevExpress.XtraSplashScreen.SplashScreenManager.ShowForm(this, typeof(DXWaitForm), true, true, false);

            var doc = ToRichEdit.Document;
            Paragraph paragraph = doc.Paragraphs.Get(ToRichEdit.Document.CaretPosition);
            string text = doc.GetText(paragraph.Range);
            var searchResults = FromRichEdit.Document.FindAll(text, SearchOptions.None);

            if (searchResults.Count() > 0)
            {
                var firstResult = searchResults[0];
                FromRichEdit.Document.CaretPosition = firstResult.Start;
                FromRichEdit.Document.Selection = firstResult;
                var currentLineStart = FromRichEdit.Document.Paragraphs.Get(firstResult.Start).Range.Start;
                FromRichEdit.Document.CaretPosition = currentLineStart;
                FromRichEdit.ScrollToCaret();
            }

            DevExpress.XtraSplashScreen.SplashScreenManager.CloseForm(false);
        }

        private void UpBarButtonItem_ItemClick(object sender, ItemClickEventArgs e)
        {
            //ToRichEdit.SelectionStart = 0;
            //ToRichEdit.ScrollToCaret();
            //ToRichEdit.Focus();
        }
        private void DownBarButtonItem_ItemClick(object sender, ItemClickEventArgs e)
        {
            //var last = ToRichEdit.Text.LastIndexOf('\n') + 2;
            //if (last > ToRichEdit.Text.Length)
            //    last = ToRichEdit.Text.Length;

            //ToRichEdit.SelectionStart = last;
            //ToRichEdit.ScrollToCaret();
            //ToRichEdit.Focus();
        }
        private void Clear1BarButtonItem_ItemClick(object sender, ItemClickEventArgs e)
        {
            FromRichEdit.Text = string.Empty;
            SaveReg();
            ClearMemory();
        }
        private void Clear2BarButtonItem_ItemClick(object sender, ItemClickEventArgs e)
        {
            ToRichEdit.Text = string.Empty;
            ClearMemory();
        }

        private void ClearMemory()
        {
            System.Diagnostics.Process.GetCurrentProcess().MinWorkingSet = (IntPtr)3000;
        }
        private void SaveReg()
        {
            SaveToRegistry($@"{subkey}\FindAll1", Find1BarEditItem);
            SaveToRegistry($@"{subkey}\FindAll2", Find2BarEditItem);
            SaveToRegistry($@"{subkey}\FindAll3", Find3BarEditItem);

            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(subkey))
            {
                key.SetValue("Text", FromRichEdit.Text, RegistryValueKind.String);
            }
        }
        private void SaveToRegistry(string registryPath, BarEditItem barEditItem)
        {
            var comboBox = ((RepositoryItemCheckedComboBoxEdit)barEditItem.Edit);
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(registryPath))
            {
                if (key != null)
                {
                    foreach (string subKeyName in key.GetSubKeyNames())
                    {
                        key.DeleteSubKeyTree(subKeyName);
                    }
                    for (int i = 0; i < comboBox.Items.Count; i++)
                    {
                        CheckedListBoxItem item = comboBox.Items[i];
                        using (RegistryKey itemKey = key.CreateSubKey($"Item{i}"))
                        {
                            itemKey.SetValue("Item", item.Value.ToString());
                            itemKey.SetValue("Checked", item.CheckState == CheckState.Checked ? "True" : "False");
                        }
                    }
                }
            }
        }
        private void UndoBarButtonItem_ItemClick(object sender, ItemClickEventArgs e)
        {
            RestorePreviousText();
        }
        private void RedoBarButtonItem_ItemClick(object sender, ItemClickEventArgs e)
        {
            RestoreNextText();
        }

        private void AddTextToHistory(string newText)
        {
            if (textHistory.Count() == 0 || textHistory.Last() != newText)
            {
                textHistory.Add(newText);
                currentIndex++;
            }
        }

        public void RestorePreviousText()
        {
            if (currentIndex > 0)
            {
                currentIndex--;
                FromRichEdit.Text = textHistory[currentIndex];
            }
        }

        public void RestoreNextText()
        {
            if (currentIndex < textHistory.Count - 1)
            {
                currentIndex++;
                FromRichEdit.Text = textHistory[currentIndex];
            }

        }
        private void repositoryItemCheckedComboBoxEdit1_ButtonClick(object sender, ButtonPressedEventArgs e)
        {

            if (e.Button.Index == 1)
            {
                var checkedComboBox = (CheckedComboBoxEdit)sender;
                var itemsToRemoveList = ((CheckedComboBoxEdit)sender).Text.Split(',').Select(s => s.Trim()).ToList();
                for (int i = checkedComboBox.Properties.Items.Count - 1; i >= 0; i--)
                {
                    var item = checkedComboBox.Properties.Items[i];
                    if (itemsToRemoveList.Contains(item.Value))
                        checkedComboBox.Properties.Items.Remove(item);
                }
                ((CheckedComboBoxEdit)sender).Text = String.Empty;
            }
        }

        private void FromRichEdit_Leave(object sender, EventArgs e)
        {
            AddTextToHistory(FromRichEdit.Text);

        }
    }
}
