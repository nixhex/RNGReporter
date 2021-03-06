﻿/*
 * This file is part of RNG Reporter
 * Copyright (C) 2012 by Bill Young, Mike Suleski, and Andrew Ringer
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using RNGReporter.Objects;
using RNGReporter.Objects.Searchers;
using RNGReporter.Properties;

namespace RNGReporter
{
    public partial class TimeFinder3rd : Form
    {
        private static readonly object threadLock = new object();
        private FrameCompare frameCompare;
        private ulong gameTick;
        private ulong gameTickAlt;
        private DateTime gameTime;
        private ushort id;
        private List<IFrameEEggPID> iframesEEgg;
        private List<IFrameEEggPID> iframesEEggIV;
        private List<IFrameRSEggPID> iframesRSEgg;
        private FrameGenerator ivGenerator;
        private Thread[] jobs;
        private BindingSource listBindingEggEIV;
        private BindingSource listBindingEggEPID;
        private BindingSource listBindingEggRS;
        private FrameGenerator lowerGenerator;
        private ulong progressFound;
        private ulong progressSearched;
        private ulong progressTotal;
        private bool refreshQueue;
        private ushort sid;
        private FrameCompare subFrameCompare;
        private int tabPage;
        private EventWaitHandle waitHandle;

        public TimeFinder3rd(ushort id, ushort sid)
        {
            this.id = id;
            this.sid = sid;
            InitializeComponent();
        }

        public int TabPage
        {
            get { return tabPage; }
            set { tabPage = value; }
        }

        //private bool paused = false;

        public void setPause()
        {
            //paused = true;
        }

        public void setUnpause()
        {
            //paused = false;
        }

        private void PlatinumTime_Load(object sender, EventArgs e)
        {
            // Add smart comboBox items
            // Would be nice if we left these in the Designer file
            // But Visual Studio seems to like deleting them without warning

            var ability = new[]
                {
                    new ComboBoxItem("Any", -1),
                    new ComboBoxItem("Ability 0", 0),
                    new ComboBoxItem("Ability 1", 1)
                };

            cbMethod.Items.AddRange(new object[]
                {
                    new ComboBoxItem("Method 1", FrameType.Method1),
                    new ComboBoxItem("Method 2", FrameType.Method2),
                    new ComboBoxItem("Method 4", FrameType.Method4),
                    new ComboBoxItem("Method H-1", FrameType.MethodH1),
                    new ComboBoxItem("Method H-2", FrameType.MethodH2),
                    new ComboBoxItem("Method H-4", FrameType.MethodH4),
                });

            cbEncounterType.Items.AddRange(new object[]
                {
                    new ComboBoxItem("Wild Pokémon", EncounterType.Wild),
                    new ComboBoxItem("Wild Pokémon (Surfing)",
                                     EncounterType.WildSurfing),
                    new ComboBoxItem("Wild Pokémon (Old Rod)",
                                     EncounterType.WildOldRod),
                    new ComboBoxItem("Wild Pokémon (Good Rod)",
                                     EncounterType.WildGoodRod),
                    new ComboBoxItem("Wild Pokémon (Super Rod)",
                                     EncounterType.WildSuperRod),
                    new ComboBoxItem("Stationary Pokémon", EncounterType.Stationary),
                    new ComboBoxItem("Safari Zone", EncounterType.SafariZone)
                });

            comboBoxShiny3rdNature.Items.AddRange(Nature.NatureDropDownCollectionSearchNatures());
            comboBoxShiny3rdAbility.DataSource = ability;
            comboBoxShiny3rdGender.DataSource = GenderFilter.GenderFilterCollection();

            comboEPIDNature.Items.AddRange(Nature.NatureDropDownCollectionSearchNatures());
            comboEPIDAbility.DataSource = ability;
            comboEPIDGender.DataSource = GenderFilter.GenderFilterCollection();

            cbNature.Items.AddRange(Nature.NatureDropDownCollectionSearchNatures());
            cbAbility.DataSource = ability;
            comboBoxGenderXD.DataSource = GenderFilter.GenderFilterCollection();

            var everstoneList = new BindingSource {DataSource = Nature.NatureDropDownCollectionSynch()};
            comboEPIDEverstone.DataSource = everstoneList;
            cbSynchNature.DataSource = Nature.NatureDropDownCollectionSynch();

            cbCapGender.DataSource = GenderFilter.GenderFilterCollection();

            Settings.Default.PropertyChanged += ChangeLanguage;
            SetLanguage();

            comboBoxShiny3rdNature.SelectedIndex = 0;
            comboBoxShiny3rdAbility.SelectedIndex = 0;
            comboEPIDGender.SelectedIndex = 0;
            comboEPIDEverstone.SelectedIndex = 0;
            comboEPIDNature.SelectedIndex = 0;
            comboEPIDCompatibility.SelectedIndex = 0;
            comboEPIDAbility.SelectedIndex = 0;

            comboBoxShiny3rdGender.SelectedIndex = 0;
            comboBoxParentCompatibility.SelectedIndex = 0;

            cbMethod.SelectedIndex = 0;
            cbEncounterType.SelectedIndex = 0;
            cbEncounterSlot.SelectedIndex = 0;
            cbSynchNature.SelectedIndex = 0;
            cbNature.SelectedIndex = 0;

            dataGridViewShinyRSResults.AutoGenerateColumns = false;
            shiny3rdPID.DefaultCellStyle.Format = "X8";

            dataGridViewEIVs.AutoGenerateColumns = false;
            dataGridViewEPIDs.AutoGenerateColumns = false;
            EPIDPID.DefaultCellStyle.Format = "X8";

            dataGridViewXDCalibration.AutoGenerateColumns = false;
            XDSeed.DefaultCellStyle.Format = "X8";
            XDPID.DefaultCellStyle.Format = "X8";
            XDTime.DefaultCellStyle.Format = "0.000";
            comboBoxNatureXD.SelectedIndex = 0;
            comboBoxGenderXD.SelectedIndex = 8;

            maskedTextBoxShiny3rdID.Text = id.ToString("00000");
            maskedTextBoxShiny3rdSID.Text = sid.ToString("00000");

            tabControl.SelectTab(tabPage);

            cbEncounterSlot.CheckBoxItems[0].Checked = true;
            cbEncounterSlot.CheckBoxItems[0].Checked = false;

            maskedTextBoxShiny3rdSID.Text = Settings.Default.SID;
            maskedTextBoxShiny3rdID.Text = Settings.Default.ID;
            textEPIDSID.Text = Settings.Default.SID;
            textEPIDID.Text = Settings.Default.ID;
            txtSID.Text = Settings.Default.SID;
            txtID.Text = Settings.Default.ID;
        }

        public void ChangeLanguage(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Language")
            {
                SetLanguage();
            }
        }

        public void SetLanguage()
        {
            var CellStyle = new DataGridViewCellStyle();
            switch ((Language) Settings.Default.Language)
            {
                case (Language.Japanese):
                    CellStyle.Font = new Font("Meiryo", 7.25F);
                    if (CellStyle.Font.Name != "Meiryo")
                    {
                        CellStyle.Font = new Font("Arial Unicode MS", 8.25F);
                        if (CellStyle.Font.Name != "Arial Unicode MS")
                        {
                            CellStyle.Font = new Font("MS Mincho", 8.25F);
                        }
                    }
                    break;
                case (Language.Korean):
                    CellStyle.Font = new Font("Malgun Gothic", 8.25F);
                    if (CellStyle.Font.Name != "Malgun Gothic")
                    {
                        CellStyle.Font = new Font("Gulim", 9.25F);
                        if (CellStyle.Font.Name != "Gulim")
                        {
                            CellStyle.Font = new Font("Arial Unicode MS", 8.25F);
                        }
                    }
                    break;
                default:
                    CellStyle.Font = DefaultFont;
                    break;
            }

            shiny3rdNature.DefaultCellStyle = CellStyle;
            EPIDNature.DefaultCellStyle = CellStyle;
            comboBoxShiny3rdNature.Font = CellStyle.Font;
            comboEPIDNature.Font = CellStyle.Font;

            for (int checkBoxIndex = 1; checkBoxIndex < comboBoxShiny3rdNature.Items.Count; checkBoxIndex++)
            {
                comboBoxShiny3rdNature.CheckBoxItems[checkBoxIndex].Text =
                    (comboBoxShiny3rdNature.CheckBoxItems[checkBoxIndex].ComboBoxItem).ToString();
                comboBoxShiny3rdNature.CheckBoxItems[checkBoxIndex].Font = CellStyle.Font;

                comboEPIDNature.CheckBoxItems[checkBoxIndex].Text =
                    (comboEPIDNature.CheckBoxItems[checkBoxIndex].ComboBoxItem).ToString();
                comboEPIDNature.CheckBoxItems[checkBoxIndex].Font = CellStyle.Font;

                cbNature.CheckBoxItems[checkBoxIndex].Text =
                    (cbNature.CheckBoxItems[checkBoxIndex].ComboBoxItem).ToString();
                cbNature.CheckBoxItems[checkBoxIndex].Font = CellStyle.Font;
            }

            comboBoxShiny3rdNature.CheckBoxItems[0].Checked = true;
            comboBoxShiny3rdNature.CheckBoxItems[0].Checked = false;

            comboEPIDNature.CheckBoxItems[0].Checked = true;
            comboEPIDNature.CheckBoxItems[0].Checked = false;

            cbNature.CheckBoxItems[0].Checked = true;
            cbNature.CheckBoxItems[0].Checked = false;

            dataGridViewShinyRSResults.Refresh();
        }

        private void PlatinumTime_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;

            if (jobs != null)
            {
                for (int i = 0; i < jobs.Length; i++)
                {
                    if (jobs[i] != null)
                    {
                        jobs[i].Abort();
                    }
                }
            }
            Hide();
        }

        private void contextMenuStripEggPid3rd_Opening(object sender, CancelEventArgs e)
        {
            if (dataGridViewShinyRSResults.SelectedRows.Count == 0)
            {
                e.Cancel = true;
            }
        }

        //Ruby/Sapphire generation
        private void Generate3rdGenRSJob()
        {
            uint searchRange = ivGenerator.MaxResults;

            //  This is where we actually go ahead and call our 
            //  generator for a list of egg PIDs based on parameters
            //  that have been passed in.
            List<Frame> frames = lowerGenerator.Generate(frameCompare, id, sid);
            progressTotal += (ulong) frames.Count*searchRange;

            //  Now we need to iterate through each result heref
            //  and create a collection of the information that
            //  we are going to place into our grid.
            foreach (Frame frame in frames)
            {
                waitHandle.WaitOne();
                ivGenerator.StaticPID = frame.Pid;
                List<Frame> shinyFrames = ivGenerator.Generate(subFrameCompare, id, sid);

                progressSearched += searchRange;
                progressFound += (uint) shinyFrames.Count;

                foreach (Frame shinyFrame in shinyFrames)
                {
                    var iframe = new IFrameRSEggPID
                        {
                            FrameLowerPID = frame.Number,
                            FrameUpperPID = shinyFrame.Number,
                            Pid = shinyFrame.Pid,
                            Shiny = shinyFrame.Shiny,
                            DisplayHp = shinyFrame.DisplayHpAlt,
                            DisplayAtk = shinyFrame.DisplayAtkAlt,
                            DisplayDef = shinyFrame.DisplayDefAlt,
                            DisplaySpa = shinyFrame.DisplaySpaAlt,
                            DisplaySpd = shinyFrame.DisplaySpdAlt,
                            DisplaySpe = shinyFrame.DisplaySpeAlt,
                            DisplayHpInh = shinyFrame.DisplayHp,
                            DisplayAtkInh = shinyFrame.DisplayAtk,
                            DisplayDefInh = shinyFrame.DisplayDef,
                            DisplaySpaInh = shinyFrame.DisplaySpa,
                            DisplaySpdInh = shinyFrame.DisplaySpd,
                            DisplaySpeInh = shinyFrame.DisplaySpe
                        };

                    lock (threadLock)
                    {
                        iframesRSEgg.Add(iframe);
                    }
                    refreshQueue = true;
                }
            }
        }

        //Emerald
        private void Generate3rdGenEPIDJob(uint calibration, uint minRedraws, uint maxRedraws)
        {
            uint searchRange = lowerGenerator.MaxResults;

            for (uint redraws = minRedraws; redraws <= maxRedraws; ++redraws)
            {
                // note: this is inefficent and should be done in a much faster way
                // will require a restructure of FrameGenerator
                uint offset = calibration + 3*redraws;
                lowerGenerator.Calibration = offset;
                List<Frame> frames = lowerGenerator.Generate(frameCompare, id, sid);
                progressTotal = (ulong) frames.Count*searchRange*(maxRedraws - minRedraws);

                foreach (Frame frame in frames)
                {
                    waitHandle.WaitOne();
                    progressSearched += searchRange;
                    progressFound += 1;
                    var iframe = new IFrameEEggPID
                        {
                            Advances = frame.Advances,
                            FrameLowerPID = frame.Number - offset,
                            Pid = frame.Pid,
                            Shiny = frame.Shiny,
                            Redraws = redraws
                        };

                    lock (threadLock)
                    {
                        iframesEEgg.Add(iframe);
                    }
                    refreshQueue = true;
                }
            }
        }

        private void Generate3rdGenEIVJob()
        {
            uint searchRange = ivGenerator.MaxResults;

            //generate the iv frames
            List<Frame> ivFrames = ivGenerator.Generate(subFrameCompare, id, sid);
            progressTotal = (ulong) ivFrames.Count*searchRange;

            foreach (Frame frame in ivFrames)
            {
                waitHandle.WaitOne();
                //ivGenerator.StaticPID = frame.Pid;
                progressSearched += searchRange;
                progressFound += (uint) ivFrames.Count;

                var iframe = new IFrameEEggPID
                    {
                        FrameNumber = frame.Name,
                        FrameUpperPID = frame.Number,
                        Pid = frame.Pid,
                        Shiny = frame.Shiny,
                        DisplayHp = frame.DisplayHpAlt,
                        DisplayAtk = frame.DisplayAtkAlt,
                        DisplayDef = frame.DisplayDefAlt,
                        DisplaySpa = frame.DisplaySpaAlt,
                        DisplaySpd = frame.DisplaySpdAlt,
                        DisplaySpe = frame.DisplaySpeAlt,
                        DisplayHpInh = frame.DisplayHp,
                        DisplayAtkInh = frame.DisplayAtk,
                        DisplayDefInh = frame.DisplayDef,
                        DisplaySpaInh = frame.DisplaySpa,
                        DisplaySpdInh = frame.DisplaySpd,
                        DisplaySpeInh = frame.DisplaySpe,
                    };

                lock (threadLock)
                {
                    iframesEEggIV.Add(iframe);
                }
                refreshQueue = true;
            }
        }

        private void ManageProgress(BindingSource bindingSource, DoubleBufferedDataGridView grid, FrameType frameType,
                                    int sleepTimer)
        {
            var progress = new Progress();
            progress.SetupAndShow(this, 0, 0, false, true, waitHandle);

            progressSearched = 0;
            progressFound = 0;

            UpdateGridDelegate gridUpdater = UpdateGrid;
            var updateParams = new object[] {bindingSource};
            ResortGridDelegate gridSorter = ResortGrid;
            var sortParams = new object[] {bindingSource, grid, frameType};
            ThreadDelegate enableGenerateButton = EnableCapGenerate;

            try
            {
                bool alive = true;
                while (alive)
                {
                    progress.ShowProgress(progressSearched/(float) progressTotal, progressSearched, progressFound);
                    if (refreshQueue)
                    {
                        Invoke(gridUpdater, updateParams);
                        refreshQueue = false;
                    }
                    if (jobs != null)
                    {
                        foreach (Thread job in jobs)
                        {
                            if (job != null && job.IsAlive)
                            {
                                alive = true;
                                break;
                            }
                            alive = false;
                        }
                    }
                    if (sleepTimer > 0)
                        Thread.Sleep(sleepTimer);
                }
            }
            catch (ObjectDisposedException)
            {
                // This keeps the program from crashing when the Time Finder progress box
                // is closed from the Windows taskbar.
            }
            catch (Exception exception)
            {
                if (exception.Message != "Operation Cancelled")
                {
                    throw;
                }
            }
            finally
            {
                progress.Finish();

                if (jobs != null)
                {
                    for (int i = 0; i < jobs.Length; i++)
                    {
                        if (jobs[i] != null)
                        {
                            jobs[i].Abort();
                        }
                    }
                }

                Invoke(enableGenerateButton);
                Invoke(gridSorter, sortParams);
            }
        }

        // Methods we'll use when we roll up the above functions

        private void UpdateGrid(BindingSource bindingSource)
        {
            bindingSource.ResetBindings(false);
        }

        private void ResortGrid(BindingSource bindingSource, DoubleBufferedDataGridView dataGrid, FrameType frameType)
        {
            switch (frameType)
            {
                case FrameType.EBredPID:
                    var iframeComparer = new IFrameEEggPIDComparer {CompareType = "Frame"};
                    ((List<IFrameEEggPID>) bindingSource.DataSource).Sort(iframeComparer);
                    EPIDFrame.HeaderCell.SortGlyphDirection = SortOrder.Ascending;
                    break;
            }
            dataGrid.DataSource = bindingSource;
            bindingSource.ResetBindings(false);
        }

        private void EnableCapGenerate()
        {
            buttonShiny3rdGenerate.Enabled = true;
        }

        private void dataGridViewShiny3rdResults_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            //  Make all of the junk natures show up in a lighter color
            if (dataGridViewShinyRSResults.Columns[e.ColumnIndex].Name == "shiny3rdNature")
            {
                var nature = (string) e.Value;

                if (nature == Functions.NatureStrings(18) ||
                    nature == Functions.NatureStrings(6) ||
                    nature == Functions.NatureStrings(0) ||
                    nature == Functions.NatureStrings(24) ||
                    nature == Functions.NatureStrings(12) ||
                    nature == Functions.NatureStrings(9) ||
                    nature == Functions.NatureStrings(21))
                {
                    e.CellStyle.ForeColor = Color.Gray;
                }
            }

            if (dataGridViewShinyRSResults.Columns[e.ColumnIndex].Name == "shiny3rdHP" ||
                dataGridViewShinyRSResults.Columns[e.ColumnIndex].Name == "shiny3rdAtk" ||
                dataGridViewShinyRSResults.Columns[e.ColumnIndex].Name == "shiny3rdDef" ||
                dataGridViewShinyRSResults.Columns[e.ColumnIndex].Name == "shiny3rdSpA" ||
                dataGridViewShinyRSResults.Columns[e.ColumnIndex].Name == "shiny3rdSpD" ||
                dataGridViewShinyRSResults.Columns[e.ColumnIndex].Name == "shiny3rdSpe")
            {
                if ((string) e.Value == "30" || (string) e.Value == "31")
                {
                    e.CellStyle.Font = new Font(e.CellStyle.Font, FontStyle.Bold);
                }

                if ((string) e.Value == "0")
                {
                    e.CellStyle.ForeColor = Color.Red;
                }

                if ((string) e.Value == "A" || (string) e.Value == "B")
                {
                    e.CellStyle.ForeColor = Color.Blue;
                }
            }
        }

        private void outputShiny3rdResultsToTXTToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //  Going to need to present the user with a File Dialog and 
            //  then interate through the Grid, outputting columns that
            //  are visible.

            saveFileDialogTxt.AddExtension = true;
            saveFileDialogTxt.Title = "Save Output to TXT";
            saveFileDialogTxt.Filter = "TXT Files|*.txt";
            saveFileDialogTxt.FileName = "rngreporter.txt";
            if (saveFileDialogTxt.ShowDialog() == DialogResult.OK)
            {
                //  Get the name of the file and then go ahead 
                //  and create and save the thing to the hard
                //  drive.   
                List<IFrameRSEggPID> frames = iframesRSEgg;

                if (frames != null)
                {
                    if (frames.Count > 0)
                    {
                        var writer = new TXTWriter(dataGridViewShinyRSResults);
                        writer.Generate(saveFileDialogTxt.FileName, frames);
                    }
                }
            }
        }

        private void dataGridViewShiny3rdResults_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                DataGridView.HitTestInfo Hti = dataGridViewShinyRSResults.HitTest(e.X, e.Y);

                if (Hti.Type == DataGridViewHitTestType.Cell)
                {
                    if (!((dataGridViewShinyRSResults.Rows[Hti.RowIndex])).Selected)
                    {
                        dataGridViewShinyRSResults.ClearSelection();

                        (dataGridViewShinyRSResults.Rows[Hti.RowIndex]).Selected = true;
                    }
                }
            }
        }

        private void buttonShiny3rdGenerate_Click(object sender, EventArgs e)
        {
            // seed used by all Ruby\Sapphire cartridges when the internal battery is dead
            const uint seed = 0x05A0;

            if (maskedTextBoxShiny3rdID.Text != "")
            {
                id = ushort.Parse(maskedTextBoxShiny3rdID.Text);
            }

            if (maskedTextBoxShiny3rdSID.Text != "")
            {
                sid = ushort.Parse(maskedTextBoxShiny3rdSID.Text);
            }

            var parentA = new uint[6];
            var parentB = new uint[6];

            uint.TryParse(maskedTextBoxShiny3rdParentA_HP.Text, out parentA[0]);
            uint.TryParse(maskedTextBoxShiny3rdParentA_Atk.Text, out parentA[1]);
            uint.TryParse(maskedTextBoxShiny3rdParentA_Def.Text, out parentA[2]);
            uint.TryParse(maskedTextBoxShiny3rdParentA_SpA.Text, out parentA[3]);
            uint.TryParse(maskedTextBoxShiny3rdParentA_SpD.Text, out parentA[4]);
            uint.TryParse(maskedTextBoxShiny3rdParentA_Spe.Text, out parentA[5]);

            uint.TryParse(maskedTextBoxShiny3rdParentB_HP.Text, out parentB[0]);
            uint.TryParse(maskedTextBoxShiny3rdParentB_Atk.Text, out parentB[1]);
            uint.TryParse(maskedTextBoxShiny3rdParentB_Def.Text, out parentB[2]);
            uint.TryParse(maskedTextBoxShiny3rdParentB_SpA.Text, out parentB[3]);
            uint.TryParse(maskedTextBoxShiny3rdParentB_SpD.Text, out parentB[4]);
            uint.TryParse(maskedTextBoxShiny3rdParentB_Spe.Text, out parentB[5]);

            uint maxHeldFrame;
            uint maxPickupFrame;
            uint minHeldFrame;
            uint minPickupFrame;

            if (!uint.TryParse(maskedTextBox3rdHeldMinFrame.Text, out minHeldFrame))
            {
                maskedTextBox3rdHeldMinFrame.Focus();
                maskedTextBox3rdHeldMinFrame.SelectAll();
                return;
            }

            if (!uint.TryParse(maskedTextBox3rdPickupMinFrame.Text, out minPickupFrame))
            {
                maskedTextBox3rdPickupMinFrame.Focus();
                maskedTextBox3rdPickupMinFrame.SelectAll();
                return;
            }

            if (!uint.TryParse(maskedTextBox3rdHeldMaxFrame.Text, out maxHeldFrame))
            {
                maskedTextBox3rdHeldMaxFrame.Focus();
                maskedTextBox3rdHeldMaxFrame.SelectAll();
                return;
            }

            if (!uint.TryParse(maskedTextBox3rdPickupMaxFrame.Text, out maxPickupFrame))
            {
                maskedTextBox3rdPickupMaxFrame.Focus();
                maskedTextBox3rdPickupMaxFrame.SelectAll();
                return;
            }

            if (minHeldFrame > maxHeldFrame)
            {
                maskedTextBox3rdHeldMinFrame.Focus();
                maskedTextBox3rdHeldMinFrame.SelectAll();
                return;
            }

            if (minPickupFrame > maxPickupFrame)
            {
                maskedTextBox3rdPickupMinFrame.Focus();
                maskedTextBox3rdPickupMinFrame.SelectAll();
                return;
            }

            lowerGenerator = new FrameGenerator();
            ivGenerator = new FrameGenerator();

            if (comboBoxParentCompatibility.SelectedIndex == 1)
            {
                lowerGenerator.Compatibility = 50;
            }
            else if (comboBoxParentCompatibility.SelectedIndex == 2)
            {
                lowerGenerator.Compatibility = 70;
            }
            else
            {
                lowerGenerator.Compatibility = 20;
            }

            lowerGenerator.FrameType = FrameType.RSBredLower;
            if (radioButtonSplitSpreads.Checked)
                ivGenerator.FrameType = FrameType.RSBredUpperSplit;
            else if (radioButtonAltSpreads.Checked)
                ivGenerator.FrameType = FrameType.RSBredUpperAlt;
            else
                ivGenerator.FrameType = FrameType.RSBredUpper;

            lowerGenerator.InitialFrame = minHeldFrame;
            ivGenerator.InitialFrame = minPickupFrame;

            lowerGenerator.MaxResults = maxHeldFrame - minHeldFrame + 1;
            ivGenerator.MaxResults = maxPickupFrame - minPickupFrame + 1;

            lowerGenerator.InitialSeed = seed;
            ivGenerator.InitialSeed = seed;

            ivGenerator.ParentA = parentA;
            ivGenerator.ParentB = parentB;

            List<uint> natures = null;
            if (comboBoxShiny3rdNature.Text != "Any" && comboBoxShiny3rdNature.CheckBoxItems.Count > 0)
            {
                natures = new List<uint>();
                for (int i = 0; i < comboBoxShiny3rdNature.CheckBoxItems.Count; i++)
                {
                    if (comboBoxShiny3rdNature.CheckBoxItems[i].Checked)
                        natures.Add((uint) ((Nature) comboBoxShiny3rdNature.CheckBoxItems[i].ComboBoxItem).Number);
                }
            }

            frameCompare = new FrameCompare(
                0, CompareType.None,
                0, CompareType.None,
                0, CompareType.None,
                0, CompareType.None,
                0, CompareType.None,
                0, CompareType.None,
                null,
                -1,
                false,
                false,
                false,
                null,
                (GenderFilter) (comboBoxShiny3rdGender.SelectedItem));

            subFrameCompare = new FrameCompare(
                parentA[0],
                parentA[1],
                parentA[2],
                parentA[3],
                parentA[4],
                parentA[5],
                parentB[0],
                parentB[1],
                parentB[2],
                parentB[3],
                parentB[4],
                parentB[5],
                ivFiltersRSEgg.IVFilter,
                natures,
                (int) ((ComboBoxItem) comboBoxShiny3rdAbility.SelectedItem).Reference,
                checkBoxShiny3rdShinyOnly.Checked,
                true,
                new NoGenderFilter());

            // Here we check the parent IVs
            // To make sure they even have a chance of producing the desired spread
            int parentPassCount = 0;
            for (int i = 0; i < 6; i++)
            {
                if (subFrameCompare.CompareIV(i, parentA[i]) ||
                    subFrameCompare.CompareIV(i, parentB[i]))
                {
                    parentPassCount++;
                }
            }

            if (parentPassCount < 3)
            {
                MessageBox.Show("The parent IVs you have listed cannot produce your desired search results.");
                return;
            }

            iframesRSEgg = new List<IFrameRSEggPID>();
            listBindingEggRS = new BindingSource {DataSource = iframesRSEgg};

            dataGridViewShinyRSResults.DataSource = listBindingEggRS;

            progressSearched = 0;
            progressFound = 0;
            progressTotal = 0;

            waitHandle = new EventWaitHandle(true, EventResetMode.ManualReset);

            jobs = new Thread[1];
            jobs[0] = new Thread(Generate3rdGenRSJob);
            jobs[0].Start();

            Thread.Sleep(200);

            var progressJob =
                new Thread(
                    () => ManageProgress(listBindingEggRS, dataGridViewShinyRSResults, lowerGenerator.FrameType, 0));
            progressJob.Start();
            progressJob.Priority = ThreadPriority.Lowest;
            buttonShiny3rdGenerate.Enabled = false;
        }

        private void checkBoxShiny3rdShowInheritance_CheckedChanged(object sender, EventArgs e)
        {
            if (!checkBoxShiny3rdShowInheritance.Checked)
            {
                shiny3rdHP.DataPropertyName = "DisplayHp";
                shiny3rdAtk.DataPropertyName = "DisplayAtk";
                shiny3rdDef.DataPropertyName = "DisplayDef";
                shiny3rdSpA.DataPropertyName = "DisplaySpa";
                shiny3rdSpD.DataPropertyName = "DisplaySpd";
                shiny3rdSpe.DataPropertyName = "DisplaySpe";
            }
            else
            {
                shiny3rdHP.DataPropertyName = "DisplayHpInh";
                shiny3rdAtk.DataPropertyName = "DisplayAtkInh";
                shiny3rdDef.DataPropertyName = "DisplayDefInh";
                shiny3rdSpA.DataPropertyName = "DisplaySpaInh";
                shiny3rdSpD.DataPropertyName = "DisplaySpdInh";
                shiny3rdSpe.DataPropertyName = "DisplaySpeInh";
            }
        }

        private void buttonGenerateXD_Click(object sender, EventArgs e)
        {
            if (maskedTextBoxMinHP.Text == "")
            {
                maskedTextBoxMinHP.Focus();
                maskedTextBoxMinHP.SelectAll();
                return;
            }

            if (maskedTextBoxMinAtk.Text == "")
            {
                maskedTextBoxMinAtk.Focus();
                maskedTextBoxMinAtk.SelectAll();
                return;
            }

            if (maskedTextBoxMinDef.Text == "")
            {
                maskedTextBoxMinDef.Focus();
                maskedTextBoxMinDef.SelectAll();
                return;
            }

            if (maskedTextBoxMinSpA.Text == "")
            {
                maskedTextBoxMinSpA.Focus();
                maskedTextBoxMinSpA.SelectAll();
                return;
            }

            if (maskedTextBoxMinSpD.Text == "")
            {
                maskedTextBoxMinSpD.Focus();
                maskedTextBoxMinSpD.SelectAll();
                return;
            }

            if (maskedTextBoxMinSpe.Text == "")
            {
                maskedTextBoxMinSpe.Focus();
                maskedTextBoxMinSpe.SelectAll();
                return;
            }

            if (maskedTextBoxMaxHP.Text == "")
            {
                maskedTextBoxMaxHP.Focus();
                maskedTextBoxMaxHP.SelectAll();
                return;
            }

            if (maskedTextBoxMaxAtk.Text == "")
            {
                maskedTextBoxMaxAtk.Focus();
                maskedTextBoxMaxAtk.SelectAll();
                return;
            }

            if (maskedTextBoxMaxDef.Text == "")
            {
                maskedTextBoxMaxDef.Focus();
                maskedTextBoxMaxDef.SelectAll();
                return;
            }

            if (maskedTextBoxMaxSpA.Text == "")
            {
                maskedTextBoxMaxSpA.Focus();
                maskedTextBoxMaxSpA.SelectAll();
                return;
            }

            if (maskedTextBoxMaxSpD.Text == "")
            {
                maskedTextBoxMaxSpD.Focus();
                maskedTextBoxMaxSpD.SelectAll();
                return;
            }

            if (maskedTextBoxMaxSpe.Text == "")
            {
                maskedTextBoxMaxSpe.Focus();
                maskedTextBoxMaxSpe.SelectAll();
                return;
            }

            uint minhp = uint.Parse(maskedTextBoxMinHP.Text);
            uint minatk = uint.Parse(maskedTextBoxMinAtk.Text);
            uint mindef = uint.Parse(maskedTextBoxMinDef.Text);
            uint minspa = uint.Parse(maskedTextBoxMinSpA.Text);
            uint minspd = uint.Parse(maskedTextBoxMinSpD.Text);
            uint minspe = uint.Parse(maskedTextBoxMinSpe.Text);

            uint maxhp = uint.Parse(maskedTextBoxMaxHP.Text);
            uint maxatk = uint.Parse(maskedTextBoxMaxAtk.Text);
            uint maxdef = uint.Parse(maskedTextBoxMaxDef.Text);
            uint maxspa = uint.Parse(maskedTextBoxMaxSpA.Text);
            uint maxspd = uint.Parse(maskedTextBoxMaxSpD.Text);
            uint maxspe = uint.Parse(maskedTextBoxMaxSpe.Text);

            var nature = (uint) comboBoxNatureXD.SelectedIndex;

            var XDGenerator = new FrameGenerator();

            List<Frame> frames = XDGenerator.Generate(minhp, maxhp,
                                                      minatk, maxatk,
                                                      mindef, maxdef,
                                                      minspa, maxspa,
                                                      minspd, maxspd,
                                                      minspe, maxspe,
                                                      nature);

            var iframes = new List<IFrameCaptureXD>();

            foreach (Frame frame in frames)
            {
                var iframe = new IFrameCaptureXD {Frame = frame};

                // We're calibrating only with shadow Pokémon that are generated first in the party.
                // There are 375451 frames between the initial seed generation and Pokémon generation,
                // so we need to reverse the RNG that many frames
                var reverseRNG = new XdRngR(frame.Seed);


                for (int i = 0; i < 375450; i++)
                {
                    reverseRNG.GetNext32BitNumber();
                }

                iframe.Seed = reverseRNG.GetNext32BitNumber();
                //iframe.Seed = frame.Seed;
                iframes.Add(iframe);
            }

            dataGridViewXDCalibration.DataSource = iframes;
        }

        private void btnSetGCTick_Click(object sender, EventArgs e)
        {
            gameTime = DateTime.Now;
            buttonGenerateXD.Enabled = true;
            btnGetCurrentTick.Enabled = true;
        }

        private void btnGetCurrentTick_Click(object sender, EventArgs e)
        {
            var tickDifference = (ulong) (DateTime.Now.Ticks - gameTime.Ticks);
            ulong currentTick = (gameTick + tickDifference/10*6)%0x100000000;
            ulong currentTickAlt = (gameTickAlt + tickDifference/10*6)%0x100000000;

            lblCurrentTick.Text = "Tick: " + currentTick.ToString("X8");
            lblCurrentTickAlt.Text = "Tick 2: " + currentTickAlt.ToString("X8");
        }

        private void buttonXDTickReset_Click(object sender, EventArgs e)
        {
            btnGetCurrentTick.Enabled = false;
            buttonGenerateXD.Enabled = false;

            maskedTextBoxXDHp.Text = "";
            maskedTextBoxXDAtk.Text = "";
            maskedTextBoxXDDef.Text = "";
            maskedTextBoxXDSpa.Text = "";
            maskedTextBoxXDSpd.Text = "";
            maskedTextBoxXDSpe.Text = "";
            textBoxXDNature.Text = "";
        }

        private void buttonXDSetStats_Click(object sender, EventArgs e)
        {
            if (dataGridViewXDCalibration.SelectedRows[0] != null)
            {
                var frame = (IFrameCaptureXD) dataGridViewXDCalibration.SelectedRows[0].DataBoundItem;
                gameTick = frame.Seed;

                var oneFrameBack = new XdRngR((uint) frame.Seed);
                gameTickAlt = oneFrameBack.GetNext32BitNumber();

                maskedTextBoxXDHp.Text = frame.DisplayHp;
                maskedTextBoxXDAtk.Text = frame.DisplayAtk;
                maskedTextBoxXDDef.Text = frame.DisplayDef;
                maskedTextBoxXDSpa.Text = frame.DisplaySpa;
                maskedTextBoxXDSpd.Text = frame.DisplaySpd;
                maskedTextBoxXDSpe.Text = frame.DisplaySpe;
                textBoxXDNature.Text = frame.Nature;
            }
        }

        private void buttonSwapParents_Click(object sender, EventArgs e)
        {
            string tempHP = maskedTextBoxShiny3rdParentB_HP.Text;
            string tempAtk = maskedTextBoxShiny3rdParentB_Atk.Text;
            string tempDef = maskedTextBoxShiny3rdParentB_Def.Text;
            string tempSpA = maskedTextBoxShiny3rdParentB_SpA.Text;
            string tempSpD = maskedTextBoxShiny3rdParentB_SpD.Text;
            string tempSpe = maskedTextBoxShiny3rdParentB_Spe.Text;

            maskedTextBoxShiny3rdParentB_HP.Text = maskedTextBoxShiny3rdParentA_HP.Text;
            maskedTextBoxShiny3rdParentB_Atk.Text = maskedTextBoxShiny3rdParentA_Atk.Text;
            maskedTextBoxShiny3rdParentB_Def.Text = maskedTextBoxShiny3rdParentA_Def.Text;
            maskedTextBoxShiny3rdParentB_SpA.Text = maskedTextBoxShiny3rdParentA_SpA.Text;
            maskedTextBoxShiny3rdParentB_SpD.Text = maskedTextBoxShiny3rdParentA_SpD.Text;
            maskedTextBoxShiny3rdParentB_Spe.Text = maskedTextBoxShiny3rdParentA_Spe.Text;

            maskedTextBoxShiny3rdParentA_HP.Text = tempHP;
            maskedTextBoxShiny3rdParentA_Atk.Text = tempAtk;
            maskedTextBoxShiny3rdParentA_Def.Text = tempDef;
            maskedTextBoxShiny3rdParentA_SpA.Text = tempSpA;
            maskedTextBoxShiny3rdParentA_SpD.Text = tempSpD;
            maskedTextBoxShiny3rdParentA_Spe.Text = tempSpe;
        }

        private void FocusControl(object sender, MouseEventArgs e)
        {
            ((Control) sender).Focus();
        }

        private void buttonGenerateEPIDs_Click(object sender, EventArgs e)
        {
            const uint seed = 0x0;

            if (textEPIDID.Text != "")
            {
                ParseInputD(textEPIDID, out id);
            }

            if (textEPIDSID.Text != "")
            {
                ParseInputD(textEPIDSID, out sid);
            }

            uint maxHeldFrame;
            uint minHeldFrame;
            uint minRedraw;
            uint maxRedraw;
            uint calibration;

            if (!ParseInputD(textEPIDMinFrame, out minHeldFrame) ||
                !ParseInputD(textEPIDMinFrame, out minHeldFrame) ||
                !ParseInputD(textEPIDMaxFrame, out maxHeldFrame) ||
                !ParseInputD(textEPIDMinRedraws, out minRedraw) ||
                !ParseInputD(textEPIDMaxRedraws, out maxRedraw) ||
                !ParseInputD(textEPIDCalibration, out calibration)) return;

            if (minHeldFrame > maxHeldFrame)
            {
                maskedTextBox3rdHeldMinFrame.Focus();
                maskedTextBox3rdHeldMinFrame.SelectAll();
                return;
            }

            lowerGenerator = new FrameGenerator();

            switch (comboEPIDCompatibility.SelectedIndex)
            {
                case 1:
                    lowerGenerator.Compatibility = 50;
                    break;
                case 2:
                    lowerGenerator.Compatibility = 70;
                    break;
                default:
                    lowerGenerator.Compatibility = 20;
                    break;
            }

            lowerGenerator.FrameType = FrameType.EBredPID;

            lowerGenerator.InitialFrame = minHeldFrame;

            lowerGenerator.MaxResults = maxHeldFrame - minHeldFrame + 1 + 3*(maxRedraw - minRedraw);

            lowerGenerator.InitialSeed = seed;

            List<uint> natures = null;
            if (comboEPIDNature.Text != "Any" && comboEPIDNature.CheckBoxItems.Count > 0)
            {
                natures = new List<uint>();
                for (int i = 0; i < comboEPIDNature.CheckBoxItems.Count; i++)
                {
                    if (comboEPIDNature.CheckBoxItems[i].Checked)
                        natures.Add((uint) ((Nature) comboEPIDNature.CheckBoxItems[i].ComboBoxItem).Number);
                }
            }

            if (comboEPIDEverstone.SelectedIndex != 0)
            {
                lowerGenerator.Everstone = true;
                lowerGenerator.SynchNature = ((Nature) comboEPIDEverstone.SelectedItem).Number;
                Advances.Visible = true;
            }
            else
                Advances.Visible = false;

            frameCompare = new FrameCompare(
                0, CompareType.None,
                0, CompareType.None,
                0, CompareType.None,
                0, CompareType.None,
                0, CompareType.None,
                0, CompareType.None,
                natures,
                (int) ((ComboBoxItem) comboEPIDAbility.SelectedItem).Reference,
                checkEPIDShiny.Checked,
                false,
                false,
                null,
                (GenderFilter) (comboEPIDGender.SelectedItem));

            // Here we check the parent IVs
            // To make sure they even have a chance of producing the desired spread
            iframesEEgg = new List<IFrameEEggPID>();
            listBindingEggEPID = new BindingSource {DataSource = iframesEEgg};
            dataGridViewEPIDs.DataSource = listBindingEggEPID;

            progressSearched = 0;
            progressFound = 0;
            progressTotal = 0;

            waitHandle = new EventWaitHandle(true, EventResetMode.ManualReset);

            jobs = new Thread[1];
            jobs[0] = new Thread(() => Generate3rdGenEPIDJob(calibration, minRedraw, maxRedraw));
            jobs[0].Start();

            Thread.Sleep(200);

            var progressJob =
                new Thread(
                    () => ManageProgress(listBindingEggEPID, dataGridViewEPIDs, lowerGenerator.FrameType, 0));
            progressJob.Start();
            progressJob.Priority = ThreadPriority.Lowest;
            buttonShiny3rdGenerate.Enabled = false;
        }

        private void buttonGenerateEIVs_Click(object sender, EventArgs e)
        {
            const uint seed = 0x0;

            var parentA = new uint[6];
            var parentB = new uint[6];

            uint.TryParse(textEIVParentA_HP.Text, out parentA[0]);
            uint.TryParse(textEIVParentA_Atk.Text, out parentA[1]);
            uint.TryParse(textEIVParentA_Def.Text, out parentA[2]);
            uint.TryParse(textEIVParentA_SpA.Text, out parentA[3]);
            uint.TryParse(textEIVParentA_SpD.Text, out parentA[4]);
            uint.TryParse(textEIVParentA_Spe.Text, out parentA[5]);

            uint.TryParse(textEIVParentB_HP.Text, out parentB[0]);
            uint.TryParse(textEIVParentB_Atk.Text, out parentB[1]);
            uint.TryParse(textEIVParentB_Def.Text, out parentB[2]);
            uint.TryParse(textEIVParentB_SpA.Text, out parentB[3]);
            uint.TryParse(textEIVParentB_SpD.Text, out parentB[4]);
            uint.TryParse(textEIVParentB_Spe.Text, out parentB[5]);

            uint maxPickupFrame;
            uint minPickupFrame;

            if (!ParseInputD(textEIVMinFrame, out minPickupFrame) || !ParseInputD(textEIVMaxFrame, out maxPickupFrame))
                return;

            if (minPickupFrame > maxPickupFrame)
            {
                textEIVMinFrame.Focus();
                textEIVMinFrame.SelectAll();
                return;
            }

            ivGenerator = new FrameGenerator();

            if (radioButtonEIVSplit.Checked)
                ivGenerator.FrameType = FrameType.BredSplit;
            else if (radioButtonEIVAlternate.Checked)
                ivGenerator.FrameType = FrameType.BredAlternate;
            else
                ivGenerator.FrameType = FrameType.Bred;

            ivGenerator.InitialFrame = minPickupFrame;

            ivGenerator.MaxResults = maxPickupFrame - minPickupFrame + 1;

            ivGenerator.InitialSeed = seed;

            ivGenerator.ParentA = parentA;
            ivGenerator.ParentB = parentB;

            subFrameCompare = new FrameCompare(
                parentA[0],
                parentA[1],
                parentA[2],
                parentA[3],
                parentA[4],
                parentA[5],
                parentB[0],
                parentB[1],
                parentB[2],
                parentB[3],
                parentB[4],
                parentB[5],
                ivFiltersEEgg.IVFilter,
                null,
                -1,
                false,
                true,
                new NoGenderFilter());

            // Here we check the parent IVs
            // To make sure they even have a chance of producing the desired spread
            int parentPassCount = 0;
            for (int i = 0; i < 6; i++)
            {
                if (subFrameCompare.CompareIV(i, parentA[i]) ||
                    subFrameCompare.CompareIV(i, parentB[i]))
                {
                    parentPassCount++;
                }
            }

            if (parentPassCount < 3)
            {
                MessageBox.Show("The parent IVs you have listed cannot produce your desired search results.");
                return;
            }

            iframesEEggIV = new List<IFrameEEggPID>();
            listBindingEggEIV = new BindingSource {DataSource = iframesEEggIV};

            dataGridViewEIVs.DataSource = listBindingEggEIV;

            progressSearched = 0;
            progressFound = 0;
            progressTotal = 0;

            waitHandle = new EventWaitHandle(true, EventResetMode.ManualReset);

            jobs = new Thread[1];
            jobs[0] = new Thread(Generate3rdGenEIVJob);
            jobs[0].Start();

            Thread.Sleep(200);

            var progressJob =
                new Thread(
                    () => ManageProgress(listBindingEggEIV, dataGridViewEIVs, ivGenerator.FrameType, 0));
            progressJob.Start();
            progressJob.Priority = ThreadPriority.Lowest;
            buttonShiny3rdGenerate.Enabled = false;
        }

        private static bool ParseInputD(TextBoxBase control, out uint value)
        {
            if (!uint.TryParse(control.Text, out value))
            {
                control.Focus();
                control.SelectAll();
                return false;
            }
            return true;
        }

        private static bool ParseInputD(TextBoxBase control, out ushort value)
        {
            if (!ushort.TryParse(control.Text, out value))
            {
                control.Focus();
                control.SelectAll();
                return false;
            }
            return true;
        }

        private void buttonAnyNature_Click(object sender, EventArgs e)
        {
            comboBoxShiny3rdNature.ClearSelection();
        }

        private void buttonAnyAbility_Click(object sender, EventArgs e)
        {
            comboBoxShiny3rdAbility.SelectedIndex = 0;
        }

        private void checkEIVInheritance_CheckedChanged(object sender, EventArgs e)
        {
            if (!checkEIVInheritance.Checked)
            {
                EIVHP.DataPropertyName = "DisplayHp";
                EIVAtk.DataPropertyName = "DisplayAtk";
                EIVDef.DataPropertyName = "DisplayDef";
                EIVSpA.DataPropertyName = "DisplaySpa";
                EIVSpD.DataPropertyName = "DisplaySpd";
                EIVSpe.DataPropertyName = "DisplaySpe";
            }
            else
            {
                EIVHP.DataPropertyName = "DisplayHpInh";
                EIVAtk.DataPropertyName = "DisplayAtkInh";
                EIVDef.DataPropertyName = "DisplayDefInh";
                EIVSpA.DataPropertyName = "DisplaySpaInh";
                EIVSpD.DataPropertyName = "DisplaySpdInh";
                EIVSpe.DataPropertyName = "DisplaySpeInh";
            }
        }

        private void buttonEIVSwapParents_Click(object sender, EventArgs e)
        {
            string tempHP = textEIVParentB_HP.Text;
            string tempAtk = textEIVParentB_Atk.Text;
            string tempDef = textEIVParentB_Def.Text;
            string tempSpA = textEIVParentB_SpA.Text;
            string tempSpD = textEIVParentB_SpD.Text;
            string tempSpe = textEIVParentB_Spe.Text;

            textEIVParentB_HP.Text = textEIVParentA_HP.Text;
            textEIVParentB_Atk.Text = textEIVParentA_Atk.Text;
            textEIVParentB_Def.Text = textEIVParentA_Def.Text;
            textEIVParentB_SpA.Text = textEIVParentA_SpA.Text;
            textEIVParentB_SpD.Text = textEIVParentA_SpD.Text;
            textEIVParentB_Spe.Text = textEIVParentA_Spe.Text;

            textEIVParentA_HP.Text = tempHP;
            textEIVParentA_Atk.Text = tempAtk;
            textEIVParentA_Def.Text = tempDef;
            textEIVParentA_SpA.Text = tempSpA;
            textEIVParentA_SpD.Text = tempSpD;
            textEIVParentA_Spe.Text = tempSpe;
        }

        private void buttonEPIDNature_Click(object sender, EventArgs e)
        {
            comboEPIDNature.ClearSelection();
        }

        private void buttonEPIDAbility_Click(object sender, EventArgs e)
        {
            comboEPIDAbility.SelectedIndex = 0;
        }

        private void cbDeadBattery_CheckedChanged(object sender, EventArgs e)
        {
            if (cbDeadBattery.Checked)
            {
                dtSeed.Enabled = false;
                txtMinHour.Enabled = false;
                txtMaxHour.Enabled = false;
                txtMinMinute.Enabled = false;
                txtMaxMinute.Enabled = false;
                //set the values for a dead battery
                dtSeed.Value = new DateTime(2000, 1, 1);
                txtMinHour.Text = "0";
                txtMaxHour.Text = "0";
                txtMinMinute.Text = "0";
                txtMaxMinute.Text = "0";
            }
            else
            {
                dtSeed.Enabled = true;
                txtMinHour.Enabled = true;
                txtMaxHour.Enabled = true;
                txtMinMinute.Enabled = true;
                txtMaxMinute.Enabled = true;
            }
        }

        private void comboBoxMethod_SelectedIndexChanged(object sender, EventArgs e)
        {
            if ((FrameType) ((ComboBoxItem) cbMethod.SelectedItem).Reference == FrameType.MethodH1 ||
                (FrameType) ((ComboBoxItem) cbMethod.SelectedItem).Reference == FrameType.MethodH2 ||
                (FrameType) ((ComboBoxItem) cbMethod.SelectedItem).Reference == FrameType.MethodH4)
            {
                cbEncounterSlot.Enabled = true;
            }
            else
            {
                cbEncounterSlot.Enabled = false;
            }
        }

        private void btnAnySlot_Click(object sender, EventArgs e)
        {
            cbEncounterSlot.ClearSelection();
        }

        private void btnClearNatures_Click(object sender, EventArgs e)
        {
            cbNature.ClearSelection();
        }

        private void buttonCapGenerate_Click(object sender, EventArgs e)
        {
            var searchParams = new Gen3SearchParams
                {
                    ability = cbAbility,
                    capButton = btnCapGenerate,
                    dataGridView = dgvCapValues,
                    date = dtSeed,
                    encounterSlot = cbEncounterSlot,
                    encounterType = cbEncounterType,
                    frameType = cbMethod,
                    gender = cbCapGender,
                    id = txtID,
                    isShiny = chkShinyOnly,
                    isSynch = chkSynchOnly,
                    ivfilters = ivFiltersCapture,
                    maxFrame = txtCapMaxFrame,
                    maxHour = txtMaxHour,
                    maxMinute = txtMaxMinute,
                    minFrame = txtCapMinFrame,
                    minHour = txtMinHour,
                    minMinute = txtMinMinute,
                    nature = cbNature,
                    sid = txtSID,
                    synchNature = cbSynchNature
                };
            Searcher searcher = new Gen3Searcher(searchParams, threadLock, this);
            if (!searcher.ParseInput()) return;
            searcher.RunSearch();
        }

        private void rbRS_CheckedChanged(object sender, EventArgs e)
        {
            if (rbRS.Checked)
            {
                cbSynchNature.Enabled = false;
                cbSynchNature.SelectedIndex = 0;
                cbDeadBattery.Enabled = true;
                cbDeadBattery_CheckedChanged(sender, e);
            }
            else
            {
                cbSynchNature.Enabled = true;
                dtSeed.Value = new DateTime(1900, 12, 31);
                dtSeed.Enabled = false;
                cbDeadBattery.Enabled = false;
                txtMinHour.Enabled = false;
                txtMaxHour.Enabled = false;
                txtMinMinute.Enabled = false;
                txtMaxMinute.Enabled = false;
            }
        }

        #region Nested type: ResortGridDelegate

        private delegate void ResortGridDelegate(
            BindingSource bindingSource, DoubleBufferedDataGridView dataGrid, FrameType frameType);

        #endregion

        #region Nested type: ThreadDelegate

        private delegate void ThreadDelegate();

        #endregion

        #region Nested type: UpdateGridDelegate

        private delegate void UpdateGridDelegate(BindingSource bindingSource);

        #endregion
    }
}