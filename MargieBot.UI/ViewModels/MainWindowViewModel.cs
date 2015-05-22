﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Bazam.WPF.UIHelpers;
using Bazam.WPF.ViewModels;
using MargieBot.MessageProcessors;
using MargieBot.Models;
using MargieBot.UI.Infrastructure.BotResponseProcessors;
using MargieBot.UI.Infrastructure.BotResponseProcessors.DnDResponseProcessors;
using MargieBot.UI.Infrastructure.Models;

namespace MargieBot.UI.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private Bot _Margie;

        private string _AuthKeySlack = string.Empty;
        public string AuthKeySlack
        {
            get { return _AuthKeySlack; }
            set { ChangeProperty<MainWindowViewModel>(vm => vm.AuthKeySlack, value); }
        }

        private string _BotUserID = string.Empty;
        public string BotUserID
        {
            get { return _BotUserID; }
            set { ChangeProperty<MainWindowViewModel>(vm => vm.BotUserID, value); }
        }

        private string _BotUserName = string.Empty;
        public string BotUserName
        {
            get { return _BotUserName; }
            set { ChangeProperty<MainWindowViewModel>(vm => vm.BotUserName, value); }
        }

        private IReadOnlyList<SlackChatHub> _ConnectedHubs;
        public IReadOnlyList<SlackChatHub> ConnectedHubs
        {
            get { return _ConnectedHubs; }
            set { ChangeProperty<MainWindowViewModel>(vm => vm.ConnectedHubs, value); }
        }

        private DateTime? _ConnectedSince = null;
        public DateTime? ConnectedSince
        {
            get { return _ConnectedSince; }
            set { ChangeProperty<MainWindowViewModel>(vm => vm.ConnectedSince, value); }
        }

        private bool _ConnectionStatus = false;
        public bool ConnectionStatus
        {
            get { return _ConnectionStatus; }
            set { ChangeProperty<MainWindowViewModel>(vm => vm.ConnectionStatus, value); }
        }

        private List<string> _Messages = new List<string>();
        public IEnumerable<string> Messages
        {
            get { return _Messages; }
        }

        private string _MessageToSend = string.Empty;
        public string MessageToSend
        {
            get { return _MessageToSend; }
            set { ChangeProperty<MainWindowViewModel>(vm => vm.MessageToSend, value); }
        }

        private SlackChatHub _SelectedChatHub;
        public SlackChatHub SelectedChatHub
        {
            get { return _SelectedChatHub; }
            set { ChangeProperty<MainWindowViewModel>(vm => vm.SelectedChatHub, value); }
        }

        private string _TeamName = string.Empty;
        public string TeamName
        {
            get { return _TeamName; }
            set { ChangeProperty<MainWindowViewModel>(vm => vm.TeamName, value); }
        }

        public ICommand ConnectCommand
        {
            get { 
                return new RelayCommand(async (timeForThings) => {
                    if (_Margie != null && ConnectionStatus) {
                        SelectedChatHub = null;
                        ConnectedHubs = null;
                        _Margie.Disconnect();
                    }
                    else {
                        // let's margie
                        _Margie = new Bot();
                        _Margie.Aliases = GetAliases();
                        foreach(KeyValuePair<string, object> value in GetStaticResponseContextData()) {
                            _Margie.ResponseContext.Add(value.Key, value.Value);
                        }
                        
                        // PROCESSOR WIREUP
                        _Margie.ResponseProcessors.AddRange(GetResponseProcessors());

                        _Margie.ConnectionStatusChanged += (bool isConnected) => {
                            ConnectionStatus = isConnected;

                            if (isConnected) {
                                // now that we're connected, build list of connected hubs for great glory
                                List<SlackChatHub> hubs = new List<SlackChatHub>();
                                hubs.AddRange(_Margie.ConnectedChannels);
                                hubs.AddRange(_Margie.ConnectedGroups);
                                hubs.AddRange(_Margie.ConnectedDMs);
                                ConnectedHubs = hubs;

                                if (ConnectedHubs.Count > 0) {
                                    SelectedChatHub = ConnectedHubs[0];
                                }

                                // also set other cool properties
                                BotUserID = _Margie.UserID;
                                BotUserName = _Margie.UserName;
                                ConnectedSince = _Margie.ConnectedSince;
                                TeamName = _Margie.TeamName;
                            }
                            else {
                                ConnectedHubs = null;
                                BotUserID = null;
                                BotUserName = null;
                                ConnectedSince = null;
                                TeamName = null;
                            }
                        };
                        _Margie.MessageReceived += (string message) => {
                            int messageCount = _Messages.Count - 500;
                            for (int i = 0; i < messageCount; i++) {
                                _Messages.RemoveAt(0);
                            }

                            _Messages.Add(message);
                            RaisePropertyChanged("Messages");
                        };

                        await _Margie.Connect(AuthKeySlack); 
                    }
                }); 
            }
        }

        public ICommand TalkCommand
        {
            get
            {
                return new RelayCommand(async (letsChatterYall) => {
                    await _Margie.Say(new BotMessage() { Text = MessageToSend, ChatHub = SelectedChatHub });
                    MessageToSend = string.Empty;
                });
            }
        }

        /// <summary>
        /// Replace the contents of the list returned from this method with any aliases you might want your bot to respond to. If you
        /// don't want your bot to respond to anything other than its actual name, just return an empty list here.
        /// </summary>
        /// <returns>A list of aliases that will cause the BotWasMentioned property of the ResponseContext to be true</returns>
        private IReadOnlyList<string> GetAliases()
        {
            return new List<string>() { "Inky" };
        }

        /// <summary>
        /// If you want to use this application to run your bot, here's where you start. Just scrap as many of the processors
        /// described in this method as you want and start fresh. Define your own resposne processors using the methods describe
        /// at https://github.com/jammerware/margiebot/wiki/Configuring-responses and return them in an IList<IResponseProcessor>. 
        /// You create them in this project, in a separate one, or even in the ExampleProcessors project if you want.
        /// 
        /// Boom! You have your own bot.
        /// </summary>
        /// <returns>A list of the processors this bot should respond with.</returns>
        private IList<IResponseProcessor> GetResponseProcessors()
        {
            // Some of these are more complicated than they need to be for the sake of example
            List<IResponseProcessor> responseProcessors = new List<IResponseProcessor>();

            // custom processors
            responseProcessors.Add(new RollResponseProcessor());
            // need to make his own
            //responseProcessors.Add(new CharacterResponseProcessor());

            // examples of simple-ish "inline" processors
            // this processor hits on Slackbot when he talks 1/4 times or so
            _Margie.ResponseProcessors.Add(_Margie.CreateResponseProcessor(
                (ResponseContext context) => { return (context.Message.User.IsSlackbot && new Random().Next(4) <= 1); },
                (ResponseContext context) => { return context.Get<Phrasebook>().GetSlackbotSalutation(); }
            ));

            // easiest one of all - this one responds if someone thanks Margie
            responseProcessors.Add(_Margie.CreateResponseProcessor(
                (ResponseContext context) => { return context.Message.MentionsBot && Regex.IsMatch(context.Message.Text, @"\b(thanks|thank you)\b", RegexOptions.IgnoreCase); },
                (ResponseContext context) => { return context.Get<Phrasebook>().GetYoureWelcome(); }
            ));

            // example of Supa Fly Mega EZ Syntactic Sugary Response Processors (not their actual name)
            _Margie
                .RespondsTo("get on that")
                .With("Pitiful mortal. I take not orders from the likes of you.")
                .With("Don't make me laugh.")
                .With("I'll consider it... worm.")
                .IfBotIsMentioned();

            // you can do these with regexes too
            _Margie
                .RespondsTo("[Ww]hat (can|do) you do", true)
                .With(@"I stand for dark elves across this realm. Also I enjoy petting Ahrek's lost backpack and cackling to myself.")
                .IfBotIsMentioned();

            // this last one just responds if someone says "hi" or whatever to the bot, but only if no other processor has responded
            responseProcessors.Add(_Margie.CreateResponseProcessor(
                (ResponseContext context) => {
                    return
                        context.Message.MentionsBot &&
                        !context.BotHasResponded &&
                        Regex.IsMatch(context.Message.Text, @"\b(hi|hey|hello|what's up|what's happening)\b", RegexOptions.IgnoreCase) &&
                        context.Message.User.ID != context.BotUserID &&
                        !context.Message.User.IsSlackbot;
                },
                (ResponseContext context) => {
                    return context.Get<Phrasebook>().GetQuery();
                }
            ));

            return responseProcessors;
        }

        /// <summary>
        /// If you want to share any data across all your processors, you can use the StaticResponseContextData property of the bot to do it. I elected
        /// to have most of my processors use a "Phrasebook" object to ensure a consistent tone across the bot's responses, so I stuff the Phrasebook
        /// into the context for use.
        /// </summary>
        /// <returns></returns>
        private static Dictionary<string, object> GetStaticResponseContextData()
        {
            return new Dictionary<string, object>() { 
                { "Phrasebook", new Phrasebook() }
            };
        }
    }
}