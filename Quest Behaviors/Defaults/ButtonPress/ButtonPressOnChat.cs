﻿// THIS BEHAVIOR IS INCOMPLETE--DO NOT USE YET

// Behavior originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//
// DOCUMENTATION:
//      http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_ButtonPressOnAura
//     
// QUICK DOX:
//      C
//
//  Parameters (required, then optional--both listed alphabetically):
//      (***One or more of the following two attributes must be specified***).
//      ButtonMTargetAuraIdN: [one entry REQUIRED] Specifies which button should be pressed when a
//              particular aura is seen on a target.  The value of M must be between 1 and 12,
//              and it represents a button position on the hotbar when the quest has replaced
//              the user's normal hotbar.  N may be omitted, or any positive integer--this
//              implies that you may have multiple auras associated with the same button.
//      QuestId [REQUIRED, Default:none]:
//
//      (***These attibutes are completely optional***)
//      ButtonOnQuestComplete [Default: none]: This specifies a button that should be pressed
//              when the quest complete.  The value for this attribute must be on the closed
//              interval of [1..12], and represents a button position on the hotbar when the quest
//              has replaced the user's normal hotbar.
//      QuestCompleteRequirement [Default:NotComplete]:
//      QuestInLogRequirement [Default:InLog]:
//              A full discussion of how the Quest* attributes operate is described in
//              http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
// 
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using CommonBehaviors.Actions;

using Styx;
using Styx.Logic;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.Logic.Profiles;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;


namespace BuddyWiki.CustomBehavior.ButtonPress.ButtonPressOnChat
{
    public class ButtonPressOnChat     : CustomForcedBehavior
    {
        public ButtonPressOnChat(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                int             tmpItemId;
                string[]        tmpPhrases;

                // Look for these phrases in the captured text...
                for (int i = 1;   i <= 12;    ++i)
                {
                    string      attributeName   = string.Format("Button{0}Phrase", i);

                    tmpPhrases = GetNumberedAttributesAsArray(attributeName, 0, ConstrainAs.StringNonEmpty, null);
                    UtilPopulateMapWithPhrases(SimpleTextToButtonMap, i, tmpPhrases);
                }


                ButtonFeedbackFail      = GetNumberedAttributesAsArray<string>("ButtonFeedbackFail", 0, ConstrainAs.StringNonEmpty, null);
                ButtonFeedbackSuccess   = GetNumberedAttributesAsArray<string>("ButtonFeedbackSuccess", 0, ConstrainAs.StringNonEmpty, null);
                ButtonOnQuestComplete   = GetAttributeAsNullable<int>("ButtonOnQuestComplete", false, ConstrainAs.HotbarButton, null);
                DebugShowText           = GetAttributeAsNullable<bool>("DebugShowText", false, null, null) ?? false;
                ExitVehicleAtQuestComplete = GetAttributeAsNullable<bool>("ExitVehicleAtQuestComplete", false, null, null) ?? true;
                tmpItemId               = GetAttributeAsNullable<int>("ItemId", false, ConstrainAs.ItemId, null) ?? 0;
                MobId                   = GetAttributeAsNullable<int>("MobId", true, ConstrainAs.MobId, null) ?? 0;
                HuntingGroundAnchor     = GetAttributeAsNullable<WoWPoint>("", false, ConstrainAs.WoWPointNonEmpty, null) ?? Me.Location;
                MonitorStartPhrases     = GetNumberedAttributesAsArray<string>("MonitorStartPhrase", 0, ConstrainAs.StringNonEmpty, null);
                MonitorStopPhrases      = GetNumberedAttributesAsArray<string>("MonitorStopPhrase", 0, ConstrainAs.StringNonEmpty, null);
                QuestId                 = GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null) ?? 0;
                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog    = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;


                // Semantic coherency...
                if (SimpleTextToButtonMap.Count() <= 0)
                {
                    LogMessage("error", "You must supply at least one 'ButtonNPhraseM' attribute.");
                    IsAttributeProblem = true;
                }

                if ((MobId != 0) && (tmpItemId != 0))
                {
                    LogMessage("error", "ItemId and MobId are mutually exclusive--please specify one, but not both.");
                    IsAttributeProblem = true;
                }


                // Install our chat handlers...
                WoWChat.MonsterEmote += HandleChatMonster;
                WoWChat.MonsterParty += HandleChatMonster;
                WoWChat.MonsterSay += HandleChatMonster;
                WoWChat.MonsterWhisper += HandleChatMonster;
                WoWChat.MonsterYell += HandleChatMonster;
                WoWChat.RaidBossEmote += HandleChatMonster;
                WoWChat.RaidBossWhisper += HandleChatMonster;
                WoWChat.RaidWarning += HandleChatLanguageSpecific;
                WoWChat.System += HandleChatSimpleMessage;

                // If no MonitoringStartPhrases, we start 'hot'...
                if (MonitorStartPhrases.Count() <= 0)
                    { _isMonitoringEnabled = true; }

                // Final initialization...
                if (tmpItemId > 0)
                {
                    Item = Me.CarriedItems.FirstOrDefault(i => i.Entry == tmpItemId);
                    if (Item == null)
                    {
                        LogMessage("error", "ItemId({0}) is not in our inventory", tmpItemId);
                        IsAttributeProblem = true; 
                    }
                }

                _behavior_HuntingGround = new HuntingGroundBehavior((messageType, format, argObjects) => LogMessage(messageType, format, argObjects),
                                                                    ViableTargets,
                                                                    HuntingGroundAnchor,
                                                                    1000.0);
			}

			catch (Exception except)
			{
				// Maintenance problems occur for a number of reasons.  The primary two are...
				// * Changes were made to the behavior, and boundary conditions weren't properly tested.
				// * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
				// In any case, we pinpoint the source of the problem area here, and hopefully it can be quickly
				// resolved.
				LogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message
									+ "\nFROM HERE:\n"
									+ except.StackTrace + "\n");
				IsAttributeProblem = true;
			}
        }


        // Attributes provided by caller...
        public string[]                 ButtonFeedbackFail { get; private set; }
        public string[]                 ButtonFeedbackSuccess { get; private set; }
        public int?                     ButtonOnQuestComplete { get; private set; }
        public bool                     DebugShowText { get; private set; }
        public bool                     ExitVehicleAtQuestComplete { get; private set; }
        public WoWPoint                 HuntingGroundAnchor { get; private set; }
        public WoWItem                  Item { get; private set; }
        public int                      MobId { get; private set; }
        public string[]                 MonitorStartPhrases { get; private set; }
        public string[]                 MonitorStopPhrases { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }

        // Private Properties & data...
        private const int               BonusActionButtonOffset         = (12 /*buttons_per_hotbar*/ * 10 /*hotbars*/);
        private WoWObject               CurrentTarget                   { get { return (_behavior_HuntingGround.CurrentTarget); }}
        private TimeSpan                Delay_WowClientLagTime          { get { return (TimeSpan.FromMilliseconds((StyxWoW.WoWClient.Latency * 2) + 150)); } }
        private TimeSpan                Delay_InputResponse             { get { return (TimeSpan.FromMilliseconds(_rand.NextDouble() * 2000)); }}                                                             
        private static LocalPlayer      Me                              { get { return (ObjectManager.Me); } }
        private readonly Dictionary<string, int> SimpleTextToButtonMap  = new Dictionary<string, int>();

        private Composite                   _behaviorRoot;
        private HuntingGroundBehavior       _behavior_HuntingGround;
        private KeyValuePair<string, int>?  _buttonAction;
        private bool                        _isBehaviorInProgress;
        private bool                        _isBehaviorDone;
        private bool                        _isDisposed;
        private bool                        _isInteracting;
        private bool                        _isMonitoringEnabled;
        private Queue<string>               _messagesPending        = new Queue<string>();
        private Random                      _rand                   = new Random();

        // Private LINQ queries...  
       private IEnumerable<WoWObject>  ViableTargets() {
                                            return (ObjectManager.GetObjectsOfType<WoWObject>(true, false)
                                                    .Where(target => (target.IsValid && (target.Entry == MobId)))
                                                    .OrderBy(target => Me.Location.SurfacePathDistance(target.Location)));
                                            }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string      SubversionId { get { return ("$Id:$"); } }
        public override string      SubversionRevision { get { return ("$Rev:$"); } }


        ~ButtonPressOnChat()
        {
            Dispose(false);
        }	

		
		public void     Dispose(bool    isExplicitlyInitiatedDispose)
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
		

        private void    HandleChatMonster(WoWChat.ChatMonsterEventArgs  args)
        {
            ProcessMessage(args.Message);
        }


        private void    HandleChatLanguageSpecific(WoWChat.ChatLanguageSpecificEventArgs    args)
        {
            ProcessMessage(args.Message);
        }


        private void    HandleChatSimpleMessage(WoWChat.ChatSimpleMessageEventArgs args)
        {
            ProcessMessage(args.Message);
        }


        private void    GuiShowProgress(string       completionReason)
        {
            if (completionReason != null)
            {
                LogMessage("info", "Behavior done (" + completionReason + ")");
                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;
                _isBehaviorDone = true;
            }
        }


        public bool     IsQuestComplete()
        {
            return (UtilIsProgressRequirementsMet(QuestId,
                                                  QuestInLogRequirement.InLog,
                                                  QuestCompleteRequirement.Complete));
        }


        private void    PendMessage(string      message)
        {
            // The server sometimes send empty messages, just ignore them...
            if (string.IsNullOrEmpty(message))
                { return; }

            _messagesPending.Enqueue(message);
        }


        private void    PressButton(int     buttonNumber)
        {
            Lua.DoString("local _,s,_ = GetActionInfo({0}) CastSpellByID(s) ",
                         buttonNumber + BonusActionButtonOffset);
        }


        private void    ProcessMessage(string       message)
        {
            if (DebugShowText && !string.IsNullOrEmpty(message))
                { LogMessage("info", "Saw message '{0}'", message); }

            // Look for our 'start monitoring' trigger phrase...
            if (!_isMonitoringEnabled && (MonitorStartPhrases.Count() > 0))
            {
                string      triggerPhrase    = MonitorStartPhrases
                                                .FirstOrDefault(phrase => message.Contains(phrase));

                if (triggerPhrase != null)
                {
                    LogMessage("debug", "Monitoring turned on by trigger phrase '{0}'", triggerPhrase);
                    _isMonitoringEnabled = true;
                }
            }

            // Look for our 'stop monitoring' trigger phrase...
            if (_isMonitoringEnabled && (MonitorStopPhrases.Count() > 0))
            {
                string      triggerPhrase    = MonitorStopPhrases
                                                .FirstOrDefault(phrase => message.Contains(phrase));

                if (triggerPhrase != null)
                {
                    LogMessage("debug", "Monitoring turned off by trigger phrase '{0}'", triggerPhrase);
                    _isMonitoringEnabled = false;
                }
            }

            // Does phrase match a button-push action?
            if (_isMonitoringEnabled)
            {
                string  phraseFail      = ButtonFeedbackFail.FirstOrDefault(phrase => message.Contains(phrase));
                string  phraseMatch     = SimpleTextToButtonMap.Keys.FirstOrDefault(phrase => message.Contains(phrase));
                string  phraseSucceed   = ButtonFeedbackSuccess.FirstOrDefault(phrase => message.Contains(phrase));

                if (phraseMatch != null)
                    { _buttonAction = new KeyValuePair<string, int>(phraseMatch, SimpleTextToButtonMap[phraseMatch]); }

                if (_buttonAction.HasValue && (phraseFail != null))
                {
                    LogMessage("debug", "Phrase '{0}' failed with Button {1}.",
                                        _buttonAction.Value.Key, _buttonAction.Value.Value);
                    _buttonAction = null;
                }

                if (_buttonAction.HasValue && (phraseSucceed != null))
                {
                    LogMessage("debug", "Phrase '{0}' succeeded with Button {1}.",
                                        _buttonAction.Value.Key, _buttonAction.Value.Value);
                    _buttonAction = null;
                }
            }
        }


        private void        UtilPopulateMapWithPhrases(Dictionary<string, int>  phraseToButtonMap,
                                                       int                      buttonNum,
                                                       string[]                 phrases)
        {
            foreach (string phrase in phrases)
            {
                if (phraseToButtonMap.ContainsKey(phrase))
                {
                    LogMessage("error", "Phrase({0}) cannot be associated with two buttons."
                                            + "  (Attempted to associate with Button{1} and Button{2}.)",
                                            phrase, phraseToButtonMap[phrase], buttonNum);
                    IsAttributeProblem = true;
                    break;
                }

                phraseToButtonMap.Add(phrase, buttonNum);
            }
        }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return (_behaviorRoot ?? (_behaviorRoot =
                new PrioritySelector(

                    // If the quest is complete, and we need to press a final button...
                    new Decorator(ret => IsQuestComplete(),
                        new Sequence(
                            new DecoratorContinue(ret => ButtonOnQuestComplete.HasValue,
                                new Sequence(
                                    new Action(delegate
                                    {
                                        TreeRoot.StatusText = string.Format("Pressing Button {0} at Quest Complete.",
                                                                            ButtonOnQuestComplete.Value);
                                        PressButton(ButtonOnQuestComplete.Value);
                                    }),
                                    new WaitContinue(Delay_WowClientLagTime, ret => false, new ActionAlwaysSucceed())
                                    )),
                                    
                            new DecoratorContinue(ret => ExitVehicleAtQuestComplete,
                                new Action(delegate
                                {
                                    LogMessage("info", "Exiting Vehicle");
                                    Lua.DoString("VehicleExit()");
                                })),

                            // If behavior done, bail...
                            // Note that this is also an implicit "is quest complete" exit criteria, also.
                            new Action(delegate
                            {
                                GuiShowProgress("quest complete");
                                _isBehaviorDone = true;
                            })
                            )),

                    // If we're to use an item, move to destination, and use it...
                    new Decorator(ret => (!_isInteracting && (Item != null)),
                        new PrioritySelector(
                            // Move to destination...
                            _behavior_HuntingGround.CreateBehavior_MoveToLocation(() => HuntingGroundAnchor),

                            // If the item is on cooldown, wait for it...
                            new Decorator(ret => (Item.Cooldown > 0),
                                new Action(delegate
                                {
                                    TreeRoot.StatusText = string.Format("Waiting for {0} cooldown in {1:0} seconds.",
                                                                        Item.Name, Item.Cooldown);
                                })),


                            // Use the item...
                            new Sequence(
                                new Action(delegate { WoWMovement.MoveStop(); }),
                                new WaitContinue(Delay_WowClientLagTime, ret=> false, new ActionAlwaysSucceed()),
                                new Action(delegate
                                {
                                    TreeRoot.StatusText = string.Format("Using Item '{0}'", Item.Name);
                                    Item.UseContainerItem();
                                    _isInteracting = true;
                                })
                            )                           
                        )),

                    // If we haven't interacted with the mob, move to it, and interact...
                    new Decorator(ret => !_isInteracting & (MobId > 0),
                        new PrioritySelector(

                            // Select and move to target...
                            _behavior_HuntingGround.CreateBehavior_SelectTarget(),
                            _behavior_HuntingGround.CreateBehavior_MoveToTarget(),

                            // Interact with target...
                            new Sequence(
                                new Action(delegate { WoWMovement.MoveStop(); }),
                                new DecoratorContinue(ret => !Me.IsSafelyFacing(CurrentTarget),
                                    new Action(delegate { WoWMovement.Face(CurrentTarget.Guid); })),
                                new Action(delegate { _behavior_HuntingGround.MobEngaged(CurrentTarget); }),
                                new WaitContinue(Delay_WowClientLagTime, ret=> false, new ActionAlwaysSucceed()),
                                new Action(delegate
                                {
                                    CurrentTarget.Interact();
                                    _isInteracting = true;
                                }),
                                new WaitContinue(Delay_WowClientLagTime, ret => false, new ActionAlwaysSucceed())
                                )
                            )),

                    // Process any new messages that have arrived...
                    new Decorator(ret => (_messagesPending.Count() > 0),
                        new Action(delegate { ProcessMessage(_messagesPending.Dequeue()); })),

                    // If we've a button to press, take action...
                    new Decorator(ret => _buttonAction.HasValue,
                        new Sequence(
                            // Need to give the WoWserver time to accept our response to the input...
                            // It also needs to have some variance, so it doesn't look like a bot's doing it.
                            new WaitContinue(Delay_InputResponse, ret => false, new ActionAlwaysSucceed()),
                            new Action(delegate
                            {
                                TreeRoot.StatusText = string.Format("Pressing button {0} due to phrase '{1}'",
                                                                    _buttonAction.Value.Value,
                                                                    _buttonAction.Value.Key);
                                PressButton(_buttonAction.Value.Value);
                                _buttonAction = null;
                            })
                        ))
                )));
        }


        public override void    Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        public override bool IsDone
        {
            get
            {
                bool    isDone      = _isBehaviorDone;

                // Once the behavior starts, the behavior alone needs to make the determination
                // of when it is completes...
                // This is needed because the behavior has some 'cleaning up' to do when the quest
                // completes.
                if (!_isBehaviorInProgress)
                {
                    isDone = isDone || !UtilIsProgressRequirementsMet(QuestId,
                                                                      QuestRequirementInLog,
                                                                      QuestRequirementComplete);
                }

                return (isDone);
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
                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = this.GetType().Name + ": " + ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");

                _isBehaviorInProgress = true;
                GuiShowProgress(null);
            }
        }

        #endregion      // Overrides of CustomForcedBehavior



        #region Mods needed for CustomForcedBehavior
        //***** CustomForcedBehavior extensions --


        // NO COPY BACK: Dummy stub...
        private string UtilLocateKey(bool isAttributeRequired,
                                    string primaryName,
                                    string[] aliasNames)
        {
            return (primaryName);
        }



        // NO COPY BACK:
        // Return true if attibuteName is composed of baseName plus an integer, or consists just of baseName.
        // Examples:    (baseName)   (attributeName)     (return value)
        //              MobId           MobId               true
        //              MobId           MobId27             true
        //              MobId           MobId27n            false
        //              MobId           MobIds              false
        private bool IsNumberedAttribute(string baseName,
                                         string attributeName)
        {
            if (!attributeName.StartsWith(baseName))
            { return (false); }

            string suffix = attributeName.Substring(baseName.Length);

            // If the attributeName exactly matches the baseName, we consider it a 'match'...
            if (suffix.Length == 0)
            { return (true); }

            // Try to convert the suffix to an integral number.  If we fail, no match...
            int tmpResult;

            if (int.TryParse(suffix, out tmpResult))
            { return (true); }

            return (false);
        }


        // COPY BACK:
        // Support method for GetNumberedAttributesAsStringArray().
        private void    AddNumberedAttributeToStringArray(IEnumerable<string> numberedAttributeNames,
                                                          string baseNamePrimary,
                                                          ref List<string> stringList)
        {
            foreach (string numberedAttributeName in numberedAttributeNames)
            {
                string  keyName     = UtilLocateKey(false, numberedAttributeName, null);
                string  tmpValue    = Args[keyName];

                if (tmpValue != null)
                {
                    // If this is an alias, warn user...
                    if (!numberedAttributeName.StartsWith(baseNamePrimary))
                    {
                        LogMessage("warning",
                                    "The attribute '{0}' is an alias for '{1}N'.\n"
                                    + "Please modify the profile to use the preferred base name of '{1}', instead.",
                                    numberedAttributeName,
                                    baseNamePrimary);
                    }

                    stringList.Add(tmpValue);
                }
                else
                { IsAttributeProblem = true; }
            }
        }


        // COPY BACK:
        /// <summary>
        /// <para>This method looks for attributes with a prefix specified as <paramref name="baseName"/>
        /// followed by an integer value (e.g., MobId1, MobId2, MobId3).  Such located parameters are collected
        /// and returned as an integer array.  An argument consisting of just the prefix and no following number
        /// (e.g., MobId) is considered as part of the acceptable set of parameters found.  An argument that does not
        /// fit the form of prefix-followed-by-number will be ignored (e.g., MobId3n, MobIds, etc).</para>
        /// <para>Attributes are also looked for under prefixes specified by <paramref name="aliasBaseNames"/></para>
        /// <para>Any found arguments must fall within the closed interval specified by <paramref name="minValue"/>
        /// and <paramref name="maxValue"/>.</para>
        /// <para>The <paramref name="countRequired"/> argument determines how many of the similarly-named attributes
        /// must be found; otherwise, an error is reported and nothing is returned.</para>
        /// </summary>
        /// <param name="baseName">a prefix used for the primary name of the arguments sought</param>
        /// <param name="countRequired">the number of similarly-named arguments that must be encountered for success</param>
        /// <param name="minValue">the minimum value the encountered argument is allowed to assume.  If the argument
        /// is smaller than minValue, an error will be reported.</param>
        /// <param name="maxValue">the maximum value an encountered argument is allowed to assume.  If the argument
        /// exceeds the maxValue, an error will be reported.</param>
        /// <param name="aliasBaseNames">alternative prefixes under which to look for acceptable attributes</param>
        /// <returns>an integer array comprised of the values of all located arguments.  This value will be null
        /// if no such parameters could be located, or the <paramref name="countRequired"/> number of arguments
        /// was not encountered.</returns>
        ///
        public string[]     GetNumberedAttributesAsStringArray(string baseName,
                                                                int countRequired,
                                                                string[] aliasBaseNames)
        {
            List<string> stringList = new List<string>();

            // Search for primary names first --
            var primaryAttributeNames = from attributeName in Args.Keys
                                        where IsNumberedAttribute(baseName, attributeName)
                                        select attributeName;

            AddNumberedAttributeToStringArray(primaryAttributeNames, baseName, ref stringList);

            // Search using alias names --
            if (aliasBaseNames != null)
            {
                var aliasAttributeNames = from aliasBaseName in aliasBaseNames
                                          from attributeName in Args.Keys
                                          where IsNumberedAttribute(aliasBaseName, attributeName)
                                          select attributeName;

                AddNumberedAttributeToStringArray(aliasAttributeNames, baseName, ref stringList);
            }

            if (stringList.Count == 0)
                { return (null); }

            if (stringList.Count < countRequired)
            {
                LogMessage("error",
                            "The attribute '{0}N' must be provided at least {1} times (saw it '{2}' times).\n"
                            + "(E.g., ButtonText1, ButtonText2, ButtonText3, ...)\n"
                            + "Please modify the profile to supply {1} attributes with a base name of '{0}'.",
                            baseName,
                            countRequired,
                            stringList.Count);

                return (null);
            }


            return (stringList.ToArray());
        }
    }

    #endregion  // Mods needed for CustomForcedBehavior


    #region Reusable behaviors
    // The behaviors in this section were designed to be efficient and robust.
    // The robustness results in some larger-than-normal, but swift code.
    // We also designed them to be reused in other behaviors--just copy, paste,
    // and call them as-needed.

    public class HuntingGroundBehavior
    {
        public delegate bool                    BehaviorFailIfNoTargetsDelegate();
        public delegate double                  DistanceDelegate();
        public delegate WoWPoint                LocationDelegate();
        public delegate void                    LoggerDelegate(string messageType, string format, params object[] args);
        public delegate IEnumerable<WoWObject>  ViableTargetsDelegate();
        public delegate WoWObject               WoWObjectDelegate();


        public HuntingGroundBehavior(LoggerDelegate            loggerDelegate,
                                     ViableTargetsDelegate     viableTargets,
                                     WoWPoint                  huntingGroundAnchor,
                                     double                    collectionDistance)
        {
            CollectionDistance = collectionDistance;
            HuntingGroundAnchor = huntingGroundAnchor;
            Logger = loggerDelegate;
            ViableTargets = viableTargets;

            UseHotspots(null);
        }


        public void         MobEngaged(WoWObject     wowObject)
        {
            if (wowObject == CurrentTarget)
                { _currentTargetAutoBlacklistTimer.Stop(); }
        }


        // Public properties...
        public double                   CollectionDistance              { get; private set; }
        public WoWObject                CurrentTarget                   { get; private set; }
        public Queue<WoWPoint>          Hotspots                        { get; set; }
        public WoWPoint                 HuntingGroundAnchor             { get; private set; }


        // Private properties & data...
        private const string            AuraName_DruidAquaticForm       = "Aquatic Form";
        private readonly TimeSpan       Delay_AutoBlacklist             = TimeSpan.FromMinutes(7);
        private readonly TimeSpan       Delay_RepopWait                 = TimeSpan.FromMilliseconds(500);
        private readonly TimeSpan       Delay_WoWClientMovementThrottle = TimeSpan.FromMilliseconds(0);
        private TimeSpan                Delay_WowClientLagTime          { get { return (TimeSpan.FromMilliseconds((StyxWoW.WoWClient.Latency * 2) + 150)); } }
        private readonly LoggerDelegate Logger;
        private static LocalPlayer      Me                              { get { return (ObjectManager.Me); } }
        private const double            MinDistanceToUse_DruidAquaticForm = 27.0;
        private int                     SpellId_DruidAquaticForm        = 1066;
        public ViableTargetsDelegate    ViableTargets                   { get; private set; }

        private TimeSpan                _currentTargetAutoBlacklistTime  = TimeSpan.FromSeconds(1);
        private readonly Stopwatch      _currentTargetAutoBlacklistTimer = new Stopwatch();
        private Queue<WoWPoint>         _hotSpots                       = new Queue<WoWPoint>();
        private WoWPoint                _huntingGroundWaitPoint;
        private readonly Stopwatch      _repopWaitingTime               = new Stopwatch();


        /// <summary>
        /// The created behavior was meant to be used in a PrioritySelector.
        /// It may also have uses inside other TreeSharp Composites.
        /// </summary>
        /// 
        /// <returns>
        /// <para>* RunStatus.Failure, if current target is viable.
        /// It will also return Failure if no targets could be located and failIfNoTargets is true</para>
        /// <para>* RunStatus.Success, if acquiring a target (or waiting for them to repop)</para>
        /// </returns>
        ///
        public Composite    CreateBehavior_SelectTarget()
        {
            return (CreateBehavior_SelectTarget(() => false));
        }

        public Composite    CreateBehavior_SelectTarget(BehaviorFailIfNoTargetsDelegate failIfNoTargets)
        {
            return (
            new PrioritySelector(

                // If we haven't engaged the mob when the auto-blacklist timer expires, give up on it and move on...
                new Decorator(ret => ((CurrentTarget != null)
                                        && (_currentTargetAutoBlacklistTimer.Elapsed > _currentTargetAutoBlacklistTime)),
                    new Action(delegate
                    {
                        Logger("warning", "Taking too long to engage '{0}'--blacklisting", CurrentTarget.Name);
                        CurrentTarget.LocallyBlacklist(Delay_AutoBlacklist);
                        CurrentTarget = null;
                    })),
                

                // If we don't have a current target, select a new one...
                // Once we select a target, its 'locked in' (unless it gets blacklisted).  This prevents us
                // from running back and forth between two equidistant targets.
                new Decorator(ret => ((CurrentTarget == null)
                                      || !CurrentTarget.IsValid
                                      || CurrentTarget.IsLocallyBlacklisted()),
                    new PrioritySelector(context => CurrentTarget = ViableTargets().FirstOrDefault(),

                        // If we found next target, we're done...
                        new Decorator(ret => (CurrentTarget != null),
                            new Action(delegate
                            {
                                _huntingGroundWaitPoint = WoWPoint.Empty;

                                if (CurrentTarget is WoWUnit)
                                    { CurrentTarget.ToUnit().Target(); }

                                _currentTargetAutoBlacklistTime = CalculateAutoBlacklistTime(CurrentTarget);
                                _currentTargetAutoBlacklistTimer.Reset();
                                _currentTargetAutoBlacklistTimer.Start();
                            })),

                        // If we've exhausted mob/object supply in area, and we need to wait, do so...
                        new Decorator(ret => !failIfNoTargets(),

                            // Move back to hunting ground anchor --
                            new PrioritySelector(

                                // If we've more than one hotspot, head to the next one...
                                new Decorator(ret => (_hotSpots.Count() > 1),
                                    new Sequence(context => FindNextHotspot(),
                                        new Action(nextHotspot => TreeRoot.StatusText = "No targets--moving to hotspot "
                                                                                        + (WoWPoint)nextHotspot),
                                        CreateBehavior_InternalMoveTo(() => FindNextHotspot())
                                        )),

                                // We find a point 'near' our anchor at which to wait...
                                // This way, if multiple people are using the same profile at the same time,
                                // they won't be standing on top of each other.
                                new Decorator(ret => (_huntingGroundWaitPoint == WoWPoint.Empty),
                                    new Action(delegate
                                        {
                                            _huntingGroundWaitPoint = HuntingGroundAnchor.FanOutRandom(CollectionDistance * 0.25);
                                            TreeRoot.StatusText = "No targets--moving near hunting ground anchor point to wait";
                                            _repopWaitingTime.Reset();
                                            _repopWaitingTime.Start();
                                        })),

                                // Move to our selected random point...
                                new Decorator(ret => (Me.Location.Distance(_huntingGroundWaitPoint) > Navigator.PathPrecision),
                                    CreateBehavior_InternalMoveTo(() => _huntingGroundWaitPoint)),

                                // Tell user what's going on...
                                new Sequence(
                                    new Action(delegate
                                        {
                                            TreeRoot.GoalText = this.GetType().Name + ": Waiting for Repops";
                                            TreeRoot.StatusText = "No targets in area--waiting for repops.  " + BuildTimeAsString(_repopWaitingTime.Elapsed);
                                        }),
                                    new WaitContinue(Delay_RepopWait, ret => false, new ActionAlwaysSucceed()))
                                ))
                        )),

                // Re-select target, if it was lost (perhaps, due to combat)...
                new Decorator(ret => ((CurrentTarget is WoWUnit) && (Me.CurrentTarget != CurrentTarget)),
                    new Action(delegate { CurrentTarget.ToUnit().Target();}))
                ));
        }


        public Composite    CreateBehavior_MoveNearTarget(WoWObjectDelegate     target,
                                                          DistanceDelegate      minRange,
                                                          DistanceDelegate      maxRange)
        {
            return (
            new PrioritySelector(context => target(),

                // If we're too far from target, move closer...
                new Decorator(wowObject => (((WoWObject)wowObject).Distance > maxRange()),
                    new Sequence(
                        new Action(wowObject =>
                        {
                            TreeRoot.StatusText = string.Format("Moving to {0} (distance: {1:0.0}) ",
                                                                ((WoWObject)wowObject).Name,
                                                                ((WoWObject)wowObject).Distance);
                        }),

                        CreateBehavior_InternalMoveTo(() => target().Location)
                    )),

                // If we're too close to target, back up...
                new Decorator(wowObject => (((WoWObject)wowObject).Distance < minRange()),
                    new PrioritySelector(

                        // If backing up, make sure we're facing the target...
                        new Decorator(ret => Me.MovementInfo.MovingBackward,
                            new Action(wowObject => WoWMovement.Face(((WoWObject)wowObject).Guid))),

                        // Start backing up...
                        new Action(wowObject =>
                        {
                            TreeRoot.StatusText = "Too close to \"" + ((WoWObject)wowObject).Name + "\"--backing up";
                            WoWMovement.MoveStop();
                            WoWMovement.Face(((WoWObject)wowObject).Guid);
                            WoWMovement.Move(WoWMovement.MovementDirection.Backwards);
                        })
                        )),

                // We're between MinRange and MaxRange, stop movement and face the target...
                new Decorator(ret => Me.IsMoving,
                    new Sequence(
                        new Action(delegate { WoWMovement.MoveStop(); }),
                        new Action(wowObject => WoWMovement.Face(((WoWObject)wowObject).Guid)),
                        new WaitContinue(Delay_WowClientLagTime, ret => false, new ActionAlwaysSucceed()),
                        new ActionAlwaysFail()      // fall through to next element
                        ))
            ));
        }


        public Composite    CreateBehavior_MoveToLocation(LocationDelegate  location)
        {
            return (
            new PrioritySelector(context => location(),

                // If we're not at location, move to it...
                new Decorator(wowPoint => (Me.Location.Distance((WoWPoint)wowPoint) > Navigator.PathPrecision),
                    new Sequence(
                        new Action(wowPoint => TreeRoot.StatusText = "Moving to location " + (WoWPoint)wowPoint),

                        CreateBehavior_InternalMoveTo(() => location())
                    ))
            ));
        }


        public Composite    CreateBehavior_MoveToTarget()
        {
            return (CreateBehavior_MoveToTarget(() => CurrentTarget));
        }

        public Composite    CreateBehavior_MoveToTarget(WoWObjectDelegate   target)
        {
            return (
            new PrioritySelector(context => target(),

                // If we're not at target, move to it...
                new Decorator(wowObject => (((WoWObject)wowObject).Distance > ((WoWObject)wowObject).InteractRange),
                    new Sequence(
                        new Action(wowObject =>
                        {
                            TreeRoot.StatusText = string.Format("Moving to {0} (distance: {1:0.0}) ",
                                                                ((WoWObject)wowObject).Name,
                                                                ((WoWObject)wowObject).Distance);
                        }),

                        CreateBehavior_InternalMoveTo(() => target().Location)
                    ))
            ));
        }


        private static string   BuildTimeAsString(TimeSpan timeSpan)
        {
            string      formatString    =  string.Empty;

            if (timeSpan.Hours > 0)
                { formatString = "{0:D2}h:{1:D2}m:{2:D2}s"; }
            else if (timeSpan.Minutes > 0)
                { formatString = "{1:D2}m:{2:D2}s"; }
            else
                { formatString = "{2:D2}s"; }

            return (string.Format(formatString, timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds));
        }


        private TimeSpan        CalculateAutoBlacklistTime(WoWObject    wowObject)
        {
            double      timeToWowObject = ((Me.Location.Distance(wowObject.Location) / Me.MovementInfo.SwimSpeed)
                                           * 2.5);     // factor of safety

            timeToWowObject = Math.Max(timeToWowObject, 20.0);  // 20sec hard lower-limit

            return (TimeSpan.FromSeconds(timeToWowObject));
        }


        private Composite       CreateBehavior_InternalMoveTo(LocationDelegate  locationDelegate)
        {
            return (
            new Sequence(context => locationDelegate(),

                // Druids, switch to Aquatic Form if swimming and distance dictates...
                new DecoratorContinue(ret => (SpellManager.CanCast(SpellId_DruidAquaticForm)
                                                && !Me.HasAura(AuraName_DruidAquaticForm)
                                                && (Me.Location.Distance(locationDelegate()) > MinDistanceToUse_DruidAquaticForm)),
                    new Action(delegate { SpellManager.Cast(SpellId_DruidAquaticForm); })),

                // Move...
                new Action(delegate
                { 
                    // Try to use Navigator to get there...
                    WoWPoint    location    = locationDelegate();
                    MoveResult  moveResult  = Navigator.MoveTo(location);

                    // If Navigator fails, fall back to click-to-move...
                    if ((moveResult == MoveResult.Failed) || (moveResult == MoveResult.PathGenerationFailed))
                        {  WoWMovement.ClickToMove(location); }
                }),

                new WaitContinue(Delay_WoWClientMovementThrottle, ret => false, new ActionAlwaysSucceed())
                )
            );
        }


        private WoWPoint        FindNearestHotspot()
        {
            WoWPoint        nearestHotspot  = _hotSpots.OrderBy(hotspot => Me.Location.Distance(hotspot)).FirstOrDefault();

            // Rotate the hotspot queue such that the nearest hotspot is on top...
            while (_hotSpots.Peek() != nearestHotspot)
            {
                WoWPoint    tmpWoWPoint     = _hotSpots.Dequeue();

                _hotSpots.Enqueue(tmpWoWPoint);
            }

            return (nearestHotspot);
        }


        private WoWPoint        FindNextHotspot()
        {
            WoWPoint    currentHotspot      = _hotSpots.Peek();

            // If we haven't reached the current hotspot, it is still the 'next' one...
            if (Me.Location.Distance(currentHotspot) > Navigator.PathPrecision)
                { return (currentHotspot); }

            // Otherwise, rotate to the next hotspot in the list...
            _hotSpots.Enqueue(currentHotspot);
            _hotSpots.Dequeue();

            return (_hotSpots.Peek());
        }


        public void         UseHotspots(IEnumerable<WoWPoint>       _hotspots)
        {
            _hotspots = _hotspots ?? new WoWPoint[0];

            _hotSpots = new Queue<WoWPoint>(_hotspots);

            if (_hotSpots.Count() <= 0)
                { _hotSpots.Enqueue(HuntingGroundAnchor); }

            FindNearestHotspot();
        }
    }

    #endregion      // Reusable behaviors


    #region Extensions to HBcore

    public class DecoratorThrottled         : Decorator
    {
        public DecoratorThrottled(TimeSpan                  throttleTime,
                                  CanRunDecoratorDelegate   canRun,
                                  Composite                 composite)
            :base(canRun, composite)
        {
            _throttleTime = throttleTime;

            _throttle = new Stopwatch();
            _throttle.Reset();
            _throttle.Start();
        }


        protected override bool CanRun(object context)
        {
            if (_throttle.Elapsed < _throttleTime)
                { return (false); }

            _throttle.Reset();
            _throttle.Start();

            return (base.CanRun(context));
        }


        private Stopwatch                   _throttle;
        private TimeSpan                    _throttleTime;
    }


    // The HBcore 'global' blacklist will also prevent looting.  We don't want that.
    // Since the HBcore blacklist is not built to instantiate, we have to roll our
    // own.  <sigh>
    public class LocalBlackList
    {
        public LocalBlackList(TimeSpan  maxSweepTime)
        {
            _maxSweepTime = maxSweepTime;
            _stopWatchForSweeping.Start();
        }

        private Dictionary<ulong, DateTime>     _blackList              = new Dictionary<ulong, DateTime>();
        private TimeSpan                        _maxSweepTime;
        private Stopwatch                       _stopWatchForSweeping   = new Stopwatch();      


        public void     Add(ulong guid,   TimeSpan timeSpan)
        {
            if (_stopWatchForSweeping.Elapsed > _maxSweepTime)
                { RemoveExpired(); }

            _blackList[guid] = DateTime.Now.Add(timeSpan);
        }


        public bool     Contains(ulong  guid)
        {
            if (_stopWatchForSweeping.Elapsed > _maxSweepTime)
                { RemoveExpired(); }

            return (_blackList.ContainsKey(guid));
        }


        public void     RemoveExpired()
        {
            DateTime    now     = DateTime.Now;

            List<ulong> expiredEntries  = (from key in _blackList.Keys
                                            where (_blackList[key] < now)
                                            select key).ToList();

            foreach (ulong entry in expiredEntries)
                { _blackList.Remove(entry); }

            _stopWatchForSweeping.Reset();
            _stopWatchForSweeping.Start();
        }
    }


    public static class WoWObject_Extensions
    {
        private static LocalPlayer      Me { get { return (ObjectManager.Me); } }

        // We provide our own 'local' blacklist.  If we use the global one maintained by HBcore,
        // that will prevent us from looting also.
        private static LocalBlackList   _blackList          = new LocalBlackList(TimeSpan.FromSeconds(30));
        private static LocalBlackList   _blackListLooting   = new LocalBlackList(TimeSpan.FromSeconds(30));

        public static void      LocallyBlacklist(this WoWObject    wowObject,
                                                 TimeSpan          timeSpan)
        {
            _blackList.Add(wowObject.Guid, timeSpan);
        }

        public static void      LootingBlacklist(this WoWObject    wowObject,
                                                 TimeSpan          timeSpan)
        {
            _blackListLooting.Add(wowObject.Guid, timeSpan);
        }


        public static bool      IsLocallyBlacklisted(this WoWObject    wowObject)
        {
            return (_blackList.Contains(wowObject.Guid));
        }

        public static bool      IsLootingBlacklisted(this WoWObject    wowObject)
        {
            return (_blackListLooting.Contains(wowObject.Guid));
        }
    }


    public static class WoWUnit_Extensions
    {
        private static LocalPlayer      Me { get { return (ObjectManager.Me); } }

        public static bool      IsInOurParty(this WoWUnit  wowUnit)
        {
            return ((Me.PartyMembers.FirstOrDefault(partyMember => (partyMember.Guid == wowUnit.Guid))) != null);
        }
    }


    public static class WoWPoint_Extensions
    {
        public static Random            _random     = new Random((int)DateTime.Now.Ticks);

        private static LocalPlayer      Me { get { return (ObjectManager.Me); } }
        public const double             TAU         = (2 * Math.PI);    // See http://tauday.com/


        public static WoWPoint  Add(this WoWPoint   wowPoint,
                                    double          x,
                                    double          y,
                                    double          z)
        {
            return (new WoWPoint((wowPoint.X + x), (wowPoint.Y + y), (wowPoint.Z + z)));
        }


        public static WoWPoint  AddPolarXY(this WoWPoint  wowPoint,
                                           double         xyHeadingInRadians,
                                           double         distance,
                                           double         zModifier)
        {
            return (wowPoint.Add((Math.Cos(xyHeadingInRadians) * distance),
                                 (Math.Sin(xyHeadingInRadians) * distance),
                                 zModifier));
        }


        // Finds another point near the destination.  Useful when toon is 'waiting' for something
        // (e.g., boat, mob repops, etc). This allows multiple people running
        // the same profile to not stand on top of each other while waiting for
        // something.
        public static WoWPoint     FanOutRandom(this WoWPoint  location,
                                                double         maxRadius)
        {
            const int           CYLINDER_LINE_COUNT     =  12;
            const int           MAX_TRIES               =  50;
            const double        SAFE_DISTANCE_BUFFER    =   1.75;

            WoWPoint            candidateDestination  = location;
            int                 tryCount;

            // Most of the time we'll find a viable spot in less than 2 tries...
            // However, if you're standing on a pier, or small platform a
            // viable alternative may take 10-15 tries--its all up to the
            // random number generator.
            for (tryCount = MAX_TRIES;   tryCount > 0;    --tryCount)
            {
                WoWPoint        circlePoint;
                bool[]          hitResults;
                WoWPoint[]      hitPoints;
                int             index;
                WorldLine[]     traceLines  = new WorldLine[CYLINDER_LINE_COUNT +1];

                candidateDestination = location.AddPolarXY((TAU * _random.NextDouble()),  (maxRadius * _random.NextDouble()),  0.0);

                // Build set of tracelines that can evaluate the candidate destination --
                // We build a cone of lines with the cone's base at the destination's 'feet',
                // and the cone's point at maxRadius over the destination's 'head'.  We also
                // include the cone 'normal' as the first entry.

                // 'Normal' vector
                index = 0;
                traceLines[index].Start = candidateDestination.Add(0.0, 0.0, maxRadius);
                traceLines[index].End   = candidateDestination.Add(0.0, 0.0, -maxRadius);

                // Cylinder vectors
                for (double turnFraction = 0.0;    turnFraction < TAU;    turnFraction += (TAU / CYLINDER_LINE_COUNT))
                {
                    ++index;
                    circlePoint = candidateDestination.AddPolarXY(turnFraction, SAFE_DISTANCE_BUFFER, 0.0);
                    traceLines[index].Start = circlePoint.Add(0.0, 0.0, maxRadius);
                    traceLines[index].End   = circlePoint.Add(0.0, 0.0, -maxRadius);
                }
                

                // Evaluate the cylinder...
                // The result for the 'normal' vector (first one) will be the location where the
                // destination meets the ground.  Before this MassTrace, only the candidateDestination's
                // X/Y values were valid.
                GameWorld.MassTraceLine(traceLines.ToArray(),
                                        GameWorld.CGWorldFrameHitFlags.HitTestGroundAndStructures,
                                        out hitResults,
                                        out hitPoints);

                candidateDestination = hitPoints[0];    // From 'normal', Destination with valid Z coordinate


                // Sanity check...
                // We don't want to be standing right on the edge of a drop-off (say we'e on
                // a plaform or pier).  If there is not solid ground all around us, we reject
                // the candidate.  Our test for validity is that the walking distance must
                // not be more than 20% greater than the straight-line distance to the point.
                int     viableVectorCount = hitPoints.Sum(point => ((Me.Location.SurfacePathDistance(point) < (Me.Location.Distance(point) * 1.20))
                                                                      ? 1
                                                                      : 0));

                if (viableVectorCount < (CYLINDER_LINE_COUNT +1))
                    { continue; }

                // If new destination is 'too close' to our current position, try again...
                if (Me.Location.Distance(candidateDestination) <= SAFE_DISTANCE_BUFFER)
                    { continue; }

                break;
            }

            // If we exhausted our tries, just go with simple destination --
            if (tryCount <= 0)
                { candidateDestination = location; }
            
            return (candidateDestination);
        }


        public static double    SurfacePathDistance(this WoWPoint     start,
                                                    WoWPoint          destination)
        {
            WoWPoint[]  groundPath = Navigator.GeneratePath(start, destination) ?? new WoWPoint[0];

            // We define an invalid path to be of 'infinite' length
            if (groundPath.Length <= 0)
                { return (double.MaxValue); }

 
            double      pathDistance = start.Distance(groundPath[0]);

            for (int i = 0;    i < (groundPath.Length - 1);    ++i)
                { pathDistance += groundPath[i].Distance(groundPath[i+1]); }

            return (pathDistance);
        }


        // Returns WoWPoint.Empty if unable to locate water's surface
        public static WoWPoint      WaterSurface(this WoWPoint      location)
        {
            WoWPoint    hitLocation;
            bool        hitResult;
            WoWPoint    locationUpper       = location.Add(0.0, 0.0, 2000.0);
            WoWPoint    locationLower       = location.Add(0.0, 0.0, -2000.0);

            hitResult = (GameWorld.TraceLine(locationUpper,
                                             locationLower,
                                             GameWorld.CGWorldFrameHitFlags.HitTestLiquid,
                                             out hitLocation)
                         || GameWorld.TraceLine(locationUpper,
                                                locationLower,
                                                GameWorld.CGWorldFrameHitFlags.HitTestLiquid2,
                                                out hitLocation));

            return (hitResult ? hitLocation : WoWPoint.Empty);
        }
    }

    #endregion      // Extensions to HBcore
}
