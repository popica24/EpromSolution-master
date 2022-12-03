﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using IniParser;
using System.Text.RegularExpressions;

namespace EpromSolution
{
    public partial class Form1 : Form
    {
        FileIniDataParser parser;
        public string InputPath;
        public IEnumerable<string> ChunkOfText;
        public int trimmed = 0;
        public int pass = 0;
        public int total = 0;
        public Form1()
        {
            InitializeComponent();
            parser = new FileIniDataParser();
            StartBtn.Enabled = false;
            InputPathLabel.TextChanged += (s, args) =>
            {
                StartBtn.Enabled = true;
            };
            OpenInput.Filter = "Text|*.txt|All|*.*";
        }

        private void OpenBtn_Click(object sender, EventArgs e)
        {
            FinalStatus.Text = "";
            try
            {
                OpenInput.ShowDialog();
                InputPath = OpenInput.FileName;
                InputPathLabel.Text = InputPath;
                ChunkOfText = File.ReadLines(InputPath);
            }
            catch (Exception ex)
            {
                InputPath = null;
                richTextBox1.Text += ex.Message;
                StartBtn.Enabled = false;
            }
            var RootFolder = String.Empty;

            using (var fs = new FileStream(InputPath, FileMode.Open, FileAccess.Read))
            {
                using (var sr = new StreamReader(fs, Encoding.UTF8))
                {
                    while (!sr.EndOfStream)
                    {
                        string line = sr.ReadLine().Trim();
                        if (line == "")
                        {
                            total++;
                            RootFolder = String.Empty;
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(RootFolder))
                            {
                                RootFolder = Directory.GetParent(line).ToString();
                           
                            }
                            else
                            {
                               
                                try
                                {
                                    EraseFile(line);
                                }
                                catch (Exception ex)
                                {
                                   // richTextBox1.Text += ex.Message + '\n';
                                   
                                }
                              
                            }
                        }
                    }
                }
                FinalStatus.Text += "Counted and trimmed " + total.ToString() + " batches";
            }
        }
        private void StartBtn_Click(object sender, EventArgs e)
        {
            richTextBox1.Text = "";
            total = 0;
            FinalStatus.Text = "";
            var RootFolder = string.Empty;
            List<string> DependentFolders = new List<string>();
            using (var fs = new FileStream(InputPath, FileMode.Open, FileAccess.Read))
            {
                using (var sr = new StreamReader(fs, Encoding.UTF8))
                {
                    while (!sr.EndOfStream)
                    {
                        string line = sr.ReadLine().Trim();
                        if (line == "")
                        {
                            total++;
                            try
                            {
                                ProcessBatch(RootFolder, DependentFolders);
                            }
                            catch (Exception ex)
                            {
                                richTextBox1.Text += ex.Message + '\n';
                                continue;
                            }
                            finally {
                                RootFolder = String.Empty;
                                DependentFolders.Clear();
                            }
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(RootFolder))
                            {
                                RootFolder = Directory.GetParent(line).ToString();
                            }
                            else
                                try
                                {
                                    DependentFolders.Add(line);
                                }
                                catch (Exception ex)
                                {
                                    continue;
                                }
                        }
                    }
                }
            }

            FinalStatus.Text +=
                "Successfully processed " + pass.ToString() + " of " + total.ToString() + "\n";
        }
        #region
        private string GetVersionName(string path)
        {
            var Folder = Directory.GetParent(path).ToString();
            var FileName = Path.GetFileName(path).ToString();
            var Digits = Regex.Match(FileName, @"\d+").Value.ToString();
            var DependentFolderContent = parser.ReadFile(Path.Combine(Folder, "contents.ini"));
            var DependentFolderString = DependentFolderContent.ToString();
            List<string> list = new List<string>(
                Regex.Split(DependentFolderString, Environment.NewLine)
            );

            if (Convert.ToInt32(Digits) == 1)
            {
                return list.FirstOrDefault(s => s.Contains("VersionName"))
                    .Substring(
                        list.FirstOrDefault(s => s.Contains("VersionName")).LastIndexOf('=') + 1
                    )
                    .Trim();
            }
            string result = list.FirstOrDefault(s => s.Contains("VersionName_v" + Digits));
            if (string.IsNullOrEmpty(result))
                return null;

            return result.Substring(result.LastIndexOf('=') + 1).Trim();
        }
        private void EraseFile(string path)
        {
            File.Delete(path);

            var ParentFolder = Directory.GetParent(path).ToString();

            var Digits = Regex.Match(Path.GetFileName(path).ToString(), @"\d+").Value.ToString();
            var DependentFolderContent = parser
                .ReadFile(Path.Combine(ParentFolder, "contents.ini"))
                .ToString();

            List<string> list = new List<string>(
                Regex.Split(DependentFolderContent, Environment.NewLine)
            );

            if (Convert.ToInt32(Digits) == 1)
            {
                var IndexToRemove = list.FirstOrDefault(s => s.Contains("VersionName ="));
                list.RemoveAt(list.IndexOf(IndexToRemove));
            }
            else
            {
                var IndexToRemove = list.FirstOrDefault(s => s.Contains("VersionName_v" + Digits));
                list.RemoveAt(list.IndexOf(IndexToRemove));
            }
            File.WriteAllLines(Path.Combine(ParentFolder, "contents.ini"), list);
        }
        private string GetVeryNextAvalabileName(string name)
        {
            var FileTextOnly = Regex.Match(name, @"^[^0-9]*").Value;
            var FileNumbersOnly = Regex.Match(name, @"\d+").Value;

            return FileTextOnly + (Convert.ToInt32(FileNumbersOnly) + 1).ToString() + ".bin";
        }
        private void InsertInFolder(string binFile, string rootFolder)
        {
            var LastName =
                "Eprom" + Directory.GetFiles(rootFolder, "*.bin").Length.ToString() + ".bin";
            var Name = GetVeryNextAvalabileName(LastName);
            File.Move(binFile, Path.Combine(rootFolder, Name));
        }
        private void InsertInContents(List<string> VersionNames, string path)
        {
            var DependentFolderContent = parser.ReadFile(path);
            var DependentFolderString = DependentFolderContent.ToString();
            List<string> list = new List<string>(
                Regex.Split(DependentFolderString, Environment.NewLine)
            );

            var LastIndexOfVersionName = list.LastOrDefault(s => s.Contains("VersionName_v"));
            int LastInfdexOfVersionNameDigits = Convert.ToInt32(
                Regex.Match(LastIndexOfVersionName, @"\d+").Value
            );
            var VNIndex = list.IndexOf(LastIndexOfVersionName) + 1;
            foreach (var entry in VersionNames)
            {
                LastInfdexOfVersionNameDigits += 1;
                list.Insert(
                    VNIndex,
                    "VersionName_v" + LastInfdexOfVersionNameDigits.ToString() + " = " + entry
                );
                VNIndex += 1;
            }
            var LastIndexOfFileName = list.LastOrDefault(s => s.Contains("Filename_v"));
            int LastIndexOfFileNameDigits = Convert.ToInt32(
                Regex.Match(LastIndexOfFileName, @"\d+").Value
            );
            var FNIndex = list.IndexOf(LastIndexOfFileName) + 1;
            for (int i = 0; i < VersionNames.Count; i++)
            {
                LastIndexOfFileNameDigits++;
                list.Insert(
                    FNIndex,
                    "Filename_v"
                        + LastIndexOfFileNameDigits.ToString()
                        + " = Eprom"
                        + LastIndexOfFileNameDigits.ToString()
                        + ".bin"
                );
                FNIndex++;
            }
            var NumVersions =
                list.IndexOf(list.LastOrDefault(s => s.Contains("Filename")))
                - list.IndexOf(list.FirstOrDefault(s => s.Contains("Filename")))
                + 1;
            list[list.IndexOf(list.LastOrDefault(s => s.Contains("NumVersions")))] =
                "NumVersions = " + NumVersions;
            File.WriteAllLines(path, list);
        }
        #endregion
        private void ProcessBatch(string rootFolder, List<string> DependentFiles)
        {
            List<string> Quoran = new List<string>();

            foreach (var F in DependentFiles)
            {
               
                foreach (var BinFile in Directory.GetFiles(Directory.GetParent(F).ToString(), "*.bin"))
                {
                    var x = BinFile;
                    var target = GetVersionName(BinFile);
                    if (string.IsNullOrEmpty(target))
                        continue;
                    else
                    {
                        Quoran.Add(target);
                        InsertInFolder(BinFile, rootFolder);
                    }
                }
                Directory.Delete(Directory.GetParent(F).ToString(), true);
            }

            InsertInContents(Quoran, Path.Combine(rootFolder, "contents.ini"));
            pass++;
            richTextBox1.Text += rootFolder + "-----------" + pass + "\n";
        }
        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {
            richTextBox1.SelectionStart = richTextBox1.Text.Length;
            richTextBox1.ScrollToCaret();
        }
    }
}
