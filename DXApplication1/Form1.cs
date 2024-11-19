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
            ResizeFormToScreenPercentage(0.75);

            //FromRichEdit.Views.SimpleView.AllowDisplayLineNumbers = true;
            //ToRichEdit.Views.SimpleView.AllowDisplayLineNumbers = true;
            //FromRichEdit.Views.SimpleView.Padding = new PortablePadding(40, 4, 4, 4);
            //ToRichEdit.Views.SimpleView.Padding = new PortablePadding(40, 4, 4, 4);

            textHistory = new List<string>();

            var process = Environment.Is64BitProcess ? "x64" : "x86";
            Text += $" - v{version} ({process})";

            splitContainerControl1.SplitterPosition = splitContainerControl1.Height / 12 * 4;

            new[] { repositoryItemCheckedComboBoxEdit1, repositoryItemCheckedComboBoxEdit2, repositoryItemCheckedComboBoxEdit3 }
                .Select((item, index) => new { item, color = colors[index] })
                .ToList()
                .ForEach(x => x.item.Appearance.ForeColor = x.color);

            new[] { barStaticItem2, barStaticItem3, barStaticItem4 }
                .Select((item, index) => new { item, color = colors[index] })
                .ToList()
                .ForEach(x => x.item.Appearance.ForeColor = x.color);
        }

        private void Form1_Activated(object sender, EventArgs e)
        {
            if (!formLoaded)
            {
                Application.DoEvents();
                formLoaded = true;

                new[] { Find1BarEditItem, Find2BarEditItem, Find3BarEditItem }
                    .Select((item, index) => $@"{subkey}\FindAll{index + 1}")
                    .ToList()
                    .ForEach(key => LoadFromRegistry(key, new[] { Find1BarEditItem, Find2BarEditItem, Find3BarEditItem }[key.Last() - '1']));

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

            new[]{
                new { EditValue = Find1BarEditItem.EditValue, Repository = repositoryItemCheckedComboBoxEdit1 },
                new { EditValue = Find2BarEditItem.EditValue, Repository = repositoryItemCheckedComboBoxEdit2 },
                new { EditValue = Find3BarEditItem.EditValue, Repository = repositoryItemCheckedComboBoxEdit3 }}
                .ToList()
                .ForEach(x => AddItemToRepository(Convert.ToString(x.EditValue), x.Repository));
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

            ToRichEdit.Text = string.Join("\n", l.Distinct()).TrimEnd('\n').TrimEnd('\r');
            //Document document = ToRichEdit.Document;
            //document.DefaultParagraphProperties.LineSpacingType = ParagraphLineSpacing.Multiple;
            //document.DefaultParagraphProperties.LineSpacingMultiplier = 1.2f;

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
                        if (hitCount == 0)
                            return string.Empty;
                        term = term.Length > 40 ? term.Substring(0, 37) + "..." : term;
                        return $"{term} ({hitCount} hits)";
                    }).Where(f => !string.IsNullOrEmpty(f)).ToList());
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
                DocumentRange range = document.Range;
                CharacterProperties defaultProps = document.BeginUpdateCharacters(range);
                defaultProps.ForeColor = System.Drawing.SystemColors.ScrollBar;
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
            //Document document = FromRichEdit.Document;
            //document.DefaultParagraphProperties.LineSpacingType = ParagraphLineSpacing.Multiple;
            //document.DefaultParagraphProperties.LineSpacingMultiplier = 1.2f;
            AddTextToHistory(text);
            DevExpress.XtraSplashScreen.SplashScreenManager.CloseForm(false);
        }
        private void FromRichEdit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.V)
            {
                SetText(Clipboard.GetText().TrimEnd('\n').TrimEnd('\r'));
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
            new[] { Find1BarEditItem, Find2BarEditItem, Find3BarEditItem }
              .Select((item, index) => new { Key = $@"{subkey}\FindAll{index + 1}", Item = item })
              .ToList()
              .ForEach(x => SaveToRegistry(x.Key, x.Item));

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
                var args = new XtraMessageBoxArgs()
                {
                    Caption = "Confirmation",
                    Text = $"Are you sure you want to delete?",
                    Buttons = new DialogResult[] { DialogResult.Yes, DialogResult.No },
                };
                if (XtraMessageBox.Show(args) == DialogResult.No)
                    return;

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
        private void UpToBarButtonItem_ItemClick(object sender, ItemClickEventArgs e)
        {
            MoveCaretToParagraph(false, ToRichEdit);
        }
        private void DownToBarButtonItem_ItemClick(object sender, ItemClickEventArgs e)
        {
            MoveCaretToParagraph(true, ToRichEdit);
        }
        private void UpFromBarButtonItem_ItemClick(object sender, ItemClickEventArgs e)
        {
            MoveCaretToParagraph(false, FromRichEdit);
        }
        private void DownFromBarButtonItem_ItemClick(object sender, ItemClickEventArgs e)
        {
            MoveCaretToParagraph(true, FromRichEdit);

        }
        private void MoveCaretToParagraph(bool moveToLastParagraph, RichEditControl richEditControl)
        {
            Document document = richEditControl.Document;
            Paragraph targetParagraph = moveToLastParagraph
                ? document.Paragraphs[document.Paragraphs.Count - 1]
                : document.Paragraphs[0];
            DocumentPosition startOfTargetParagraph = targetParagraph.Range.Start;
            richEditControl.Document.CaretPosition = startOfTargetParagraph;
            richEditControl.ScrollToCaret();
        }
        private void TagToBarButtonItem_ItemClick(object sender, ItemClickEventArgs e)
        {
            TagDocument(ToRichEdit);
        }
        private void TagFromBarButtonItem_ItemClick(object sender, ItemClickEventArgs e)
        {
            TagDocument(FromRichEdit);
        }
        private void TagDocument(RichEditControl richEditControl)
        {
            Document document = richEditControl.Document;
            DocumentPosition caretPosition = document.CaretPosition;
            int paragraphIndex = document.Paragraphs.Get(caretPosition).Index;
            Paragraph selectedParagraph = document.Paragraphs[paragraphIndex];
            CharacterProperties properties = document.BeginUpdateCharacters(selectedParagraph.Range);
            Color defaultColor = Color.Transparent;
            Color highlightColor = Color.FromArgb(33, 66, 131);
            if (properties.BackColor == highlightColor)
                properties.BackColor = defaultColor;
            else
                properties.BackColor = highlightColor;
            document.EndUpdateCharacters(properties);
        }
        private void RichEdit_DocumentLoaded(object sender, EventArgs e)
        {
            Section section = ((RichEditControl)sender).Document.Sections[0];
            SectionLineNumbering lineNumbering = section.LineNumbering;
            lineNumbering.CountBy = 1;
            lineNumbering.Start = 1;
            lineNumbering.Distance = 0.1f;
            lineNumbering.RestartType = LineNumberingRestart.NewSection;
        }
    }
}
