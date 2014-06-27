// Behavior originally contributed by AxaZol.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//

#region Summary and Documentation
// DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_RunMacro
//
#endregion


#region Examples
#endregion


#region Usings
using System;
using System.Collections.Generic;

using Honorbuddy.QuestBehaviorCore;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.WoWInternals;
#endregion



namespace Honorbuddy.Quest_Behaviors.RunMacro
{
	[CustomBehaviorFileName(@"RunMacro")]
	public class RunMacro : CustomForcedBehavior
	{
		public RunMacro(Dictionary<string, string> args)
			: base(args)
		{
			QBCLog.BehaviorLoggingContext = this;

			try
			{
				// QuestRequirement* attributes are explained here...
				//    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
				// ...and also used for IsDone processing.
				GoalText = GetAttributeAs<string>("GoalText", false, ConstrainAs.StringNonEmpty, null) ?? "";
				Macro = GetAttributeAs<string>("Macro", true, ConstrainAs.StringNonEmpty, null) ?? "";
				NumOfTimes = GetAttributeAsNullable<int>("NumOfTimes", false, ConstrainAs.RepeatCount, null) ?? 1;
				QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
				QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
				QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
				WaitTime = GetAttributeAsNullable<int>("WaitTime", false, ConstrainAs.Milliseconds, null) ?? 1500;

				if (string.IsNullOrEmpty(GoalText))
				{ GoalText = "Running Macro"; }
			}

			catch (Exception except)
			{
				// Maintenance problems occur for a number of reasons.  The primary two are...
				// * Changes were made to the behavior, and boundary conditions weren't properly tested.
				// * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
				// In any case, we pinpoint the source of the problem area here, and hopefully it
				// can be quickly resolved.
				QBCLog.Exception(except);
				IsAttributeProblem = true;
			}
		}


		// Attributes provided by caller
		public string GoalText { get; private set; }
		public string Macro { get; private set; }
		public int NumOfTimes { get; private set; }
		public int QuestId { get; private set; }
		public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
		public QuestInLogRequirement QuestRequirementInLog { get; private set; }
		public int WaitTime { get; private set; }

		// Private variables for internal state
		private bool _isBehaviorDone;
		private bool _isDisposed;

		// DON'T EDIT THESE--they are auto-populated by Subversion
		public override string SubversionId { get { return ("$Id$"); } }
		public override string SubversionRevision { get { return ("$Revision$"); } }


		~RunMacro()
		{
			Dispose(false);
		}


		public void Dispose(bool isExplicitlyInitiatedDispose)
		{
			if (!_isDisposed)
			{
				// NOTE: we should call any Dispose() method for any managed or unmanaged
				// resource, if that resource provides a Dispose() method.

				// Clean up managed resources, if explicit disposal...
				if (isExplicitlyInitiatedDispose)
				{
					// empty, for now
				}

				// Clean up unmanaged resources (if any) here...
				TreeRoot.GoalText = string.Empty;
				TreeRoot.StatusText = string.Empty;

				// Call parent Dispose() (if it exists) here ...
				base.Dispose();
			}

			_isDisposed = true;
		}


		#region Overrides of CustomForcedBehavior

		public override void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}


		public override bool IsDone
		{
			get
			{
				return (_isBehaviorDone     // normal completion
						|| !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
			}
		}


		public override void OnStart()
		{
			// This reports problems, and stops BT processing if there was a problem with attributes...
			// We had to defer this action, as the 'profile line number' is not available during the element's
			// constructor call.
			OnStart_HandleAttributeProblem();

			// If the quest is complete, this behavior is already done...
			// So we don't want to falsely inform the user of things that will be skipped.
			if (!IsDone)
			{
				for (int counter = 1; counter <= NumOfTimes; ++counter)
				{
					this.UpdateGoalText(QuestId, string.Format("Running macro {0} times", NumOfTimes));
					TreeRoot.StatusText = string.Format("RunMacro {0}/{1} Times", counter, NumOfTimes);

					Lua.DoString(string.Format("RunMacroText(\"{0}\")", Macro), 0);
					StyxWoW.Sleep(WaitTime);
				}

				_isBehaviorDone = true;
			}
		}

		#endregion
	}
}