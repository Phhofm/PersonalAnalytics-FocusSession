﻿// Created by Sebastian Müller (smueller@ifi.uzh.ch) from the University of Zurich
// Created: 2017-03-15
// 
// Licensed under the MIT License.

using Shared;
using Shared.Data;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using TaskDetectionTracker.Data;
using TaskDetectionTracker.Model;
using TaskDetectionTracker.Views;

namespace TaskDetectionTracker
{
    public class Deamon : BaseTracker, ITracker
    {
        private DispatcherTimer _popUpTimer;
        private DispatcherTimer _popUpReminderTimer;

        private DateTime _lastPopUpResponse = DateTime.MinValue;

        #region ITracker Stuff

        public Deamon()
        {
            Name = Settings.TrackerName;
        }

        public override void Start()
        {
            if (_popUpTimer != null || _popUpReminderTimer != null)
            {
                Stop(); // stop timers
            }

            // initialize the popup timer
            _popUpTimer = new DispatcherTimer();
            _popUpTimer.Interval = Settings.PopUpInterval;
            _popUpTimer.Tick += PopUp_Tick;

            // initialize the popup reminder timer
            _popUpReminderTimer = new DispatcherTimer();
            _popUpReminderTimer.Interval = Settings.PopUpReminderInterval;
            _popUpReminderTimer.Tick += PopUpReminder_Tick;

            IsRunning = true;
        }

        public override void Stop()
        {
            if (_popUpTimer != null)
            {
                _popUpTimer.Stop();
                _popUpTimer = null;
            }
            if (_popUpReminderTimer != null)
            {
                _popUpReminderTimer.Stop();
                _popUpReminderTimer = null;
            }

            IsRunning = false;
        }

        public override void CreateDatabaseTablesIfNotExist()
        {
            DatabaseConnector.CreateTaskDetectionValidationTable();
        }

        public override string GetVersion()
        {
            var v = new AssemblyName(Assembly.GetExecutingAssembly().FullName).Version;
            return Shared.Helpers.VersionHelper.GetFormattedVersion(v);
        }

        public override bool IsEnabled()
        {
            // no settings, meaning: the user cannot disable it
            return Settings.IsEnabledByDefault;
        }

        public override void UpdateDatabaseTables(int version)
        {
            //not needed
        }

        public override string GetStatus()
        {
            var nextSurveyTs = _lastPopUpResponse.Add(Settings.PopUpInterval);
            return (!IsRunning || !_popUpTimer.IsEnabled)
                ? Name + " is NOT running"
                : Name + " is running. Next task detection validation at " + nextSurveyTs.ToShortDateString() + " " + nextSurveyTs.ToShortTimeString() + ".";
        }

        #endregion

        private void PopUp_Tick(object sender, EventArgs e)
        {
            // stop pop-up timer
            _popUpTimer.Stop();

            // load all data first
            var taskDetections = PrepareTaskDetectionDataForPopup();

            // show pop-up 
            ShowTaskDetectionValidationPopup(taskDetections);
        }

        /// <summary>
        /// Show the task detection validation for the time since the last 
        /// time the participant answered the popup
        /// </summary>
        /// <returns></returns>
        private List<TaskDetection> PrepareTaskDetectionDataForPopup()
        {
            // get session start and end
            var sessionStart = _lastPopUpResponse;
            if (_lastPopUpResponse == DateTime.MinValue || _lastPopUpResponse.Date != DateTime.Now.Date)
            {
                _lastPopUpResponse = Database.GetInstance().GetUserWorkStart(DateTime.Now.Date);
            }
            var sessionEnd = DateTime.Now;

            var processes = new List<TaskDetectionInput>(); // TODO: get list of processes (+ user input) from database
            var taskDetections = new List<TaskDetection>(); // TODO: run task detection (using Katja's helper, likely on separate thread)

            return taskDetections;
        }

        /// <summary>
        /// Shows a popup with all detected task switches and asks the user
        /// to validate them. The response is handled in a separate method.
        /// </summary>
        /// <param name="taskDetections"></param>
        private void ShowTaskDetectionValidationPopup(List<TaskDetection> taskDetections)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(
                () =>
                {
                    var popup = new TaskDetectionPopup(taskDetections);

                    // show popup & handle response
                    if (popup.ShowDialog() == true)
                    {
                        HandlePopUpResponse(popup, taskDetections);
                    }
                    else
                    {
                        // we get here when DialogResult is set to false (which never happens) 
                        Database.GetInstance().LogErrorUnknown("DialogResult of PopUp was set to false in tracker: " + Name);
                    }
                }));
            }
            catch (ThreadAbortException e) { Database.GetInstance().LogError(Name + ": " + e.Message); }
            catch (Exception e) { Logger.WriteToLogFile(e); }
        }

        /// <summary>
        /// Handles the popup response.
        /// - if answered: stores the validation in the database
        /// - else: re-opens the window and asks the user to do it again
        /// </summary>
        /// <param name="taskDetectionPopup"></param>
        /// <param name="popup"></param>
        private void HandlePopUpResponse(TaskDetectionPopup popup, List<TaskDetection> taskDetections)
        {
            if (popup.ValidationComplete)
            {
                // no need for the reminder anymore
                _popUpReminderTimer.Stop();

                // save validation responses to the database
                DatabaseConnector.TaskDetectionSession_SaveToDatabase(taskDetections);
            }
            else
            {
                // TODO: restart popup !!
            }
        }

        private void PopUpReminder_Tick(object sender, EventArgs e)
        {
            // TODO: show reminder for timespan
            // TODO: get the timespan that is still missing
        }
    }
}