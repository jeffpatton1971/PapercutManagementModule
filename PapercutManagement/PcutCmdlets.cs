﻿using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Management.Automation;
using CookComputing.XmlRpc;

namespace PapercutManagement
{
    public class Globals
    {
        public static string authToken;
        public static string ComputerName;
        public static int Port;
    }

    [Cmdlet(VerbsCommunications.Connect, "pcutServer")]
    public class Connect_PcutServer : Cmdlet
    {
        [Parameter(Mandatory = false,
            HelpMessage = "Please provide authToken")]
        public string authToken;

        [Parameter(Mandatory = true,
            HelpMessage = "Please enter the name of the papercut server")]
        [ValidateNotNullOrEmpty]
        public string ComputerName;

        [Parameter(Mandatory = false,
            HelpMessage = "Please enter the port number, or leave blank for default (9191)")]
        public int Port = 9191;

        static ServerCommandProxy _serverProxy;

        protected override void BeginProcessing()
        {
            base.BeginProcessing();
            if (authToken == null)
            {
                Console.Write("Please enter the authToken : ");
                authToken = Console.ReadLine();
                Globals.authToken = authToken;
            }
        }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            _serverProxy = new ServerCommandProxy(ComputerName, Port, authToken);
            try
            {
                int TotalUsers;
                TotalUsers = _serverProxy.GetTotalUsers();
                if (TotalUsers >= 0)
                {
                    Globals.authToken = authToken;
                    Globals.ComputerName = ComputerName;
                    Globals.Port = Port;

                    WriteObject("Connected to " + Globals.ComputerName + ":" + Globals.Port);
                }
            }
            catch (XmlRpcFaultException fex)
            {
                ErrorRecord errRecord = new ErrorRecord(new Exception(fex.Message, fex.InnerException), fex.FaultString, ErrorCategory.NotSpecified, fex);
                WriteError(errRecord);
            }
        }
    }

    [Cmdlet(VerbsCommunications.Disconnect, "pcutServer")]
    public class Disconnect_PcutServer : Cmdlet
    {
        static ServerCommandProxy _serverProxy;

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            if (Globals.authToken != null)
            {
                _serverProxy = new ServerCommandProxy(Globals.ComputerName, Globals.Port, Globals.authToken);
                try
                {
                    Globals.ComputerName = null;
                    Globals.authToken = null;
                    Globals.Port = 0;
                    WriteObject("You are disconnected from the server");
                }
                catch (XmlRpcFaultException fex)
                {
                    ErrorRecord errRecord = new ErrorRecord(new Exception(fex.Message, fex.InnerException), fex.FaultString, ErrorCategory.NotSpecified, fex);
                    WriteError(errRecord);
                }
            }
            else
            {
                WriteObject("Please run Connect-PcutServer in order to establish connection.");
            }
        }
    }

    [Cmdlet(VerbsCommon.Get, "pcutPrinter")]
    public class Get_PcutPrinter : Cmdlet
    {
        [Parameter(Mandatory = false, Position = 0,
            HelpMessage = "Please provide the current username")]
        [ValidateNotNullOrEmpty]
        public string PrinterName;

        [Parameter(Mandatory = false, 
            HelpMessage = "Please enter a number to start at (default 0)")]
        public int Offset = 0;

        [Parameter(Mandatory = false, 
            HelpMessage = "Please enter the total number of users to return (default 1000)")]
        public int Limit = 1000;

        static ServerCommandProxy _serverProxy = new ServerCommandProxy(Globals.ComputerName, Globals.Port, Globals.authToken);
        string[] pcutPrinters = null;

        protected override void BeginProcessing()
        {
            if (Globals.authToken == null)
            {
                Exception myException = new Exception("Please run Connect-PcutServer in order to establish connection", new Exception("No connection established"));
                ErrorCategory myCategory = new ErrorCategory();
                ErrorRecord myError = new ErrorRecord(myException, "101", myCategory, this);
                this.ThrowTerminatingError(myError);
            }
            else
            {
                try
                {
                    if (PrinterName == null)
                    {
                        pcutPrinters = _serverProxy.ListPrinters(Offset, Limit);
                    }
                    else
                    {
                        int Counter = Limit;
                        do
                        {
                            Limit = Counter;
                            WriteDebug("Counter == " + Counter);
                            pcutPrinters = _serverProxy.ListPrinters(Offset, Counter);
                            WriteDebug("pcutPrinters.Length == " + pcutPrinters.Length);
                            if (pcutPrinters.Length == Counter)
                            {
                                Counter += 1000;
                                WriteDebug("Counter == " + Counter);
                            }
                        } while ((pcutPrinters.Length == Limit));
                        WriteDebug("pcutPrinters.Length == " + pcutPrinters.Length);
                    }
                }
                catch (XmlRpcFaultException fex)
                {
                    ErrorRecord errRecord = new ErrorRecord(new Exception(fex.Message, fex.InnerException), fex.FaultString, new ErrorCategory(), fex);
                    WriteError(errRecord);
                }
                catch (Exception ex)
                {
                    ErrorRecord errRecord = new ErrorRecord(new Exception(ex.Message, ex.InnerException), ex.HResult.ToString(), new ErrorCategory(), ex);
                }

                if (PrinterName != null)
                {
                    pcutPrinters = Array.FindAll(pcutPrinters, element => element.ToUpper().Contains(PrinterName.ToUpper()));
                }
            }
        }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            string printServer = null;
            string printerName = null;
            string printerDisabled = null;
            string printerJobCount = null;
            string printerPageCount = null;
            string printerCostModel = null;

            foreach (string pcutPrinter in pcutPrinters)
            {
                try
                {
                    if (!(pcutPrinter.Substring(0, 2) == "!!"))
                    {
                        string[] pcutResult = pcutPrinter.Split('\\');
                        printServer = pcutResult[0];
                        printerName = pcutResult[1];
                        printerDisabled = _serverProxy.GetPrinterProperty(printServer, printerName, "disabled");
                        printerJobCount = _serverProxy.GetPrinterProperty(printServer, printerName, "print-stats.job-count");
                        printerPageCount = _serverProxy.GetPrinterProperty(printServer, printerName, "print-stats.page-count");
                        printerCostModel = _serverProxy.GetPrinterProperty(printServer, printerName, "cost-model");

                        PSObject thisPrinter = new PSObject();
                        thisPrinter.Properties.Add(new PSNoteProperty("Name", printerName));
                        thisPrinter.Properties.Add(new PSNoteProperty("Server", printServer));
                        thisPrinter.Properties.Add(new PSNoteProperty("Disabled", Convert.ToBoolean(printerDisabled)));
                        thisPrinter.Properties.Add(new PSNoteProperty("JobCount", Convert.ToInt32(printerJobCount)));
                        thisPrinter.Properties.Add(new PSNoteProperty("PageCount", Convert.ToInt32(printerPageCount)));
                        thisPrinter.Properties.Add(new PSNoteProperty("CostModel", printerCostModel));
                        WriteObject(thisPrinter);
                    }
                }
                catch (XmlRpcFaultException fex)
                {
                    ErrorRecord errRecord = new ErrorRecord(new Exception(fex.Message, fex.InnerException), fex.FaultString, new ErrorCategory(), fex);
                    WriteError(errRecord);
                }
                catch (Exception ex)
                {
                    ErrorRecord errRecord = new ErrorRecord(new Exception(ex.Message, ex.InnerException), ex.HResult.ToString(), new ErrorCategory(), ex);
                }
            }
        }

        protected override void EndProcessing()
        {
            base.EndProcessing();
        }
    }

    [Cmdlet(VerbsCommon.Add, "pcutAdminAccessUser")]
    public class Add_PcutAdminAccessUser : Cmdlet
    {
        [Parameter(Mandatory = true,
            HelpMessage = "Please provide the current username")]
        [ValidateNotNullOrEmpty]
        public string UserName;

        static ServerCommandProxy _serverProxy;

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            if (Globals.authToken != null)
            {
                _serverProxy = new ServerCommandProxy(Globals.ComputerName, Globals.Port, Globals.authToken);
                try
                {
                    _serverProxy.AddAdminAccessUser(UserName);
                    PSObject returnAddPcutAdminAccessUser = new PSObject();
                    returnAddPcutAdminAccessUser.Properties.Add(new PSNoteProperty("Username", UserName));
                    returnAddPcutAdminAccessUser.Properties.Add(new PSNoteProperty("AdminAccess", true));
                    WriteObject(returnAddPcutAdminAccessUser);
                }
                catch (XmlRpcFaultException fex)
                {
                    ErrorRecord errRecord = new ErrorRecord(new Exception(fex.Message, fex.InnerException), fex.FaultString, ErrorCategory.NotSpecified, fex);
                    WriteError(errRecord);
                }
            }
            else
            {
                WriteObject("Please run Connect-PcutServer in order to establish connection.");
            }
        }
    }

    [Cmdlet(VerbsCommon.Remove, "pcutAdminAccessUser")]
    public class Remove_PcutAdminAccessUser : Cmdlet
    {
        [Parameter(Mandatory = true,
            HelpMessage = "Please provide the current username")]
        [ValidateNotNullOrEmpty]
        public string UserName;

        static ServerCommandProxy _serverProxy;

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            if (Globals.authToken != null)
            {
                _serverProxy = new ServerCommandProxy(Globals.ComputerName, Globals.Port, Globals.authToken);
                try
                {
                    _serverProxy.RemoveAdminAccessUser(UserName);
                    PSObject returnRemovePcutAdminAccessUser = new PSObject();
                    returnRemovePcutAdminAccessUser.Properties.Add(new PSNoteProperty("Username", UserName));
                    returnRemovePcutAdminAccessUser.Properties.Add(new PSNoteProperty("AdminAccess", true));
                    WriteObject(returnRemovePcutAdminAccessUser);
                }
                catch (XmlRpcFaultException fex)
                {
                    ErrorRecord errRecord = new ErrorRecord(new Exception(fex.Message, fex.InnerException), fex.FaultString, ErrorCategory.NotSpecified, fex);
                    WriteError(errRecord);
                }
            }
            else
            {
                WriteObject("Please run Connect-PcutServer in order to establish connection.");
            }
        }
    }

    [Cmdlet(VerbsData.Update, "pcutInternalAdminPassword")]
    public class Update_PcutInternalAdminPassword : Cmdlet
    {
        [Parameter(Mandatory = true,
            HelpMessage = "Please provide a password")]
        [ValidateNotNullOrEmpty]
        public string Password;

        static ServerCommandProxy _serverProxy;

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            if (Globals.authToken != null)
            {
                _serverProxy = new ServerCommandProxy(Globals.ComputerName, Globals.Port, Globals.authToken);
                try
                {
                    bool passwordChanged = _serverProxy.ChangeInternalAdminPassword(Password);
                    PSObject returnPasswordChanged = new PSObject();
                    returnPasswordChanged.Properties.Add(new PSNoteProperty("PasswordChanged", passwordChanged));
                    Globals.authToken = null;
                    Globals.ComputerName = null;
                    Globals.Port = 0;
                    WriteObject(returnPasswordChanged);
                }
                catch (XmlRpcFaultException fex)
                {
                    ErrorRecord errRecord = new ErrorRecord(new Exception(fex.Message, fex.InnerException), fex.FaultString, ErrorCategory.NotSpecified, fex);
                    WriteError(errRecord);
                }
            }
            else
            {
                WriteObject("Please run Connect-PcutServer in order to establish connection.");
            }
        }
    }

    [Cmdlet(VerbsCommon.Get,"pcutTotalUsers")]
    public class Get_PcutTotalUsers : Cmdlet
    {
        static ServerCommandProxy _serverProxy;

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            if (Globals.authToken != null)
            {
                _serverProxy = new ServerCommandProxy(Globals.ComputerName, Globals.Port, Globals.authToken);
                try
                {
                    int TotalUsers;
                    TotalUsers = _serverProxy.GetTotalUsers();
                    PSObject returnTotalusers = new PSObject();
                    PSNoteProperty propertyTotalUsers = new PSNoteProperty("TotalUsers", TotalUsers);
                    PSNoteProperty propertyComputerName = new PSNoteProperty("Server", Globals.ComputerName);
                    returnTotalusers.Properties.Add(propertyTotalUsers);
                    returnTotalusers.Properties.Add(propertyComputerName);
                    WriteObject(returnTotalusers);
                }
                catch (XmlRpcFaultException fex)
                {
                    ErrorRecord errRecord = new ErrorRecord(new Exception(fex.Message, fex.InnerException), fex.FaultString, ErrorCategory.NotSpecified, fex);
                    WriteError(errRecord);
                }
            }
            else
            {
                WriteObject("Please run Connect-PcutServer in order to establish connection.");
            }
        }
    }

    [Cmdlet(VerbsCommon.Get, "pcutUser")]
    public class Get_PcutUser : Cmdlet
    {
        [Parameter(Mandatory = false, Position = 0,
            HelpMessage = "Please provide the current username")]
        [ValidateNotNullOrEmpty]
        public string UserName;

        [Parameter(Mandatory = false,
            HelpMessage = "Please enter a number to start at (default 0)")]
        public int Offset = 0;

        [Parameter(Mandatory = false,
            HelpMessage = "Please enter the total number of users to return (default 1000)")]
        public int Limit = 1000;

        static ServerCommandProxy _serverProxy = new ServerCommandProxy(Globals.ComputerName, Globals.Port, Globals.authToken);
        string[] pcutUsers = null;

        protected override void BeginProcessing()
        {
            if (Globals.authToken == null)
            {
                Exception myException = new Exception("Please run Connect-PcutServer in order to establish connection", new Exception("No connection established"));
                ErrorCategory myCategory = new ErrorCategory();
                ErrorRecord myError = new ErrorRecord(myException, "101", myCategory, this);
                this.ThrowTerminatingError(myError);
            }
            else
            {
                try
                {
                    if (UserName == null)
                    {
                        pcutUsers = _serverProxy.ListUserAccounts(Offset, Limit);
                    }
                    else
                    {
                        pcutUsers = new string[] { UserName };
                    }
                }
                catch (XmlRpcFaultException fex)
                {
                    ErrorRecord errRecord = new ErrorRecord(new Exception(fex.Message, fex.InnerException), fex.FaultString, new ErrorCategory(), fex);
                    WriteError(errRecord);
                }
                catch (Exception ex)
                {
                    ErrorRecord errRecord = new ErrorRecord(new Exception(ex.Message, ex.InnerException), ex.HResult.ToString(), new ErrorCategory(), ex);
                }
            }
        }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            foreach (string pcutUser in pcutUsers)
            {
                try
                {
                    string[] userGroups = _serverProxy.GetUserGroups(pcutUser);
                    string fullName = _serverProxy.GetUserProperty(pcutUser, "full-name");
                    string email = _serverProxy.GetUserProperty(pcutUser, "email");
                    string disabledPrint = _serverProxy.GetUserProperty(pcutUser, "disabled-print");
                    string disabledNet = _serverProxy.GetUserProperty(pcutUser, "disabled-net");
                    string balance = _serverProxy.GetUserProperty(pcutUser, "balance");
                    string pageCount = _serverProxy.GetUserProperty(pcutUser, "print-stats.page-count");
                    string jobCount = _serverProxy.GetUserProperty(pcutUser, "print-stats.job-count");
                    string restricted = _serverProxy.GetUserProperty(pcutUser, "restricted");
                    string accountMode = _serverProxy.GetUserProperty(pcutUser, "account-selection.mode");
                    string department = _serverProxy.GetUserProperty(pcutUser, "department");
                    string office = _serverProxy.GetUserProperty(pcutUser, "office");
                    string cardNumber1 = _serverProxy.GetUserProperty(pcutUser, "card-number");
                    string cardNumber2 = _serverProxy.GetUserProperty(pcutUser, "secondary-card-number");
                    string cardPin = _serverProxy.GetUserProperty(pcutUser, "card-pin");
                    string notes = _serverProxy.GetUserProperty(pcutUser, "notes");

                    PSObject thisUser = new PSObject();
                    thisUser.Properties.Add(new PSNoteProperty("Username", pcutUser));
                    thisUser.Properties.Add(new PSNoteProperty("Fullname", fullName));
                    thisUser.Properties.Add(new PSNoteProperty("Group", userGroups));
                    thisUser.Properties.Add(new PSNoteProperty("Email", email));
                    thisUser.Properties.Add(new PSNoteProperty("PrintDisabled", Convert.ToBoolean(disabledPrint)));
                    thisUser.Properties.Add(new PSNoteProperty("NetDisabled", Convert.ToBoolean(disabledNet)));
                    thisUser.Properties.Add(new PSNoteProperty("Balance", Convert.ToDouble(balance)));
                    thisUser.Properties.Add(new PSNoteProperty("PageCount", Convert.ToInt32(pageCount)));
                    thisUser.Properties.Add(new PSNoteProperty("JobCount", Convert.ToInt32(jobCount)));
                    thisUser.Properties.Add(new PSNoteProperty("Restricted", Convert.ToBoolean(restricted)));
                    thisUser.Properties.Add(new PSNoteProperty("AccountMode", accountMode));
                    thisUser.Properties.Add(new PSNoteProperty("Department", department));
                    thisUser.Properties.Add(new PSNoteProperty("Office", office));
                    thisUser.Properties.Add(new PSNoteProperty("Card1", (cardNumber1)));
                    thisUser.Properties.Add(new PSNoteProperty("Card2", (cardNumber2)));
                    thisUser.Properties.Add(new PSNoteProperty("PIN", (cardPin)));
                    thisUser.Properties.Add(new PSNoteProperty("Notes", notes));
                    WriteObject(thisUser);
                }
                catch (XmlRpcFaultException fex)
                {
                    ErrorRecord errRecord = new ErrorRecord(new Exception(fex.Message, fex.InnerException), fex.FaultString, new ErrorCategory(), fex);
                    WriteError(errRecord);
                }
                catch (Exception ex)
                {
                    ErrorRecord errRecord = new ErrorRecord(new Exception(ex.Message, ex.InnerException), ex.HResult.ToString(), new ErrorCategory(), ex);
                }
            }
        }
    }

    [Cmdlet(VerbsCommon.Add, "pcutUserToGroup")]
    public class Add_PcutUserToGroup : Cmdlet
    {
        [Parameter(Mandatory = true,
            HelpMessage = "Please provide the current username")]
        [ValidateNotNullOrEmpty]
        public string UserName;

        [Parameter(Mandatory = true,
            HelpMessage = "Please provide the group name")]
        [ValidateNotNullOrEmpty]
        public string GroupName;

        static ServerCommandProxy _serverProxy;

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            if (Globals.authToken != null)
            {
                _serverProxy = new ServerCommandProxy(Globals.ComputerName, Globals.Port, Globals.authToken);
                try
                {
                    _serverProxy.AddUserToGroup(UserName, GroupName);
                    WriteObject(UserName + " added to " + GroupName);
                }
                catch (XmlRpcFaultException fex)
                {
                    ErrorRecord errRecord = new ErrorRecord(new Exception(fex.Message, fex.InnerException), fex.FaultString, ErrorCategory.NotSpecified, fex);
                    WriteError(errRecord);
                }
            }
            else
            {
                WriteObject("Please run Connect-PcutServer in order to establish connection.");
            }
        }
    }

    [Cmdlet(VerbsCommon.Remove, "pcutUserFromGroup")]
    public class Remove_PcutUserFromGroup : Cmdlet
    {
        [Parameter(Mandatory = true,
            HelpMessage = "Please provide the current username")]
        [ValidateNotNullOrEmpty]
        public string UserName;

        [Parameter(Mandatory = true,
            HelpMessage = "Please provide the group name")]
        [ValidateNotNullOrEmpty]
        public string GroupName;

        static ServerCommandProxy _serverProxy;

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            if (Globals.authToken != null)
            {
                _serverProxy = new ServerCommandProxy(Globals.ComputerName, Globals.Port, Globals.authToken);
                try
                {
                    _serverProxy.RemoveUserFromGroup(UserName, GroupName);
                    WriteObject(UserName + " removed from " + GroupName);
                }
                catch (XmlRpcFaultException fex)
                {
                    ErrorRecord errRecord = new ErrorRecord(new Exception(fex.Message, fex.InnerException), fex.FaultString, ErrorCategory.NotSpecified, fex);
                    WriteError(errRecord);
                }
            }
            else
            {
                WriteObject("Please run Connect-PcutServer in order to establish connection.");
            }
        }
    }

    [Cmdlet(VerbsCommon.Get, "pcutSharedAccount")]
    public class Get_PcutSharedAccount : Cmdlet
    {
        [Parameter(Mandatory = false,
            HelpMessage = "Please enter a number to start at (default 0)")]
        public int Offset = 0;

        [Parameter(Mandatory = false,
            HelpMessage = "Please enter the total number of users to return (default 1000)")]
        public int Limit = 1000;

        static ServerCommandProxy _serverProxy;

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            if (Globals.authToken != null)
            {
                _serverProxy = new ServerCommandProxy(Globals.ComputerName, Globals.Port, Globals.authToken);
                try
                {
                    string[] pcutSharedAccounts = _serverProxy.ListSharedAccounts(Offset, Limit);
                    Collection<PSObject> returnPcutSharedAccounts = new Collection<PSObject>();
                    foreach (string pcutSharedAccount in pcutSharedAccounts)
                    {
                        PSObject thisSharedAccount = new PSObject();
                        thisSharedAccount.Properties.Add(new PSNoteProperty("Name", pcutSharedAccount));
                        returnPcutSharedAccounts.Add(thisSharedAccount);
                    }
                    WriteObject(returnPcutSharedAccounts);
                }
                catch (XmlRpcFaultException fex)
                {
                    ErrorRecord errRecord = new ErrorRecord(new Exception(fex.Message, fex.InnerException), fex.FaultString, ErrorCategory.NotSpecified, fex);
                    WriteError(errRecord);
                }
            }
            else
            {
                WriteObject("Please run Connect-PcutServer in order to establish connection.");
            }
        }
    }

    [Cmdlet(VerbsCommon.Get, "pcutUserSharedAccount")]
    public class Get_PcutUserSharedAccount : Cmdlet
    {
        [Parameter(Mandatory = true,
            HelpMessage = "Please provide the current username")]
        [ValidateNotNullOrEmpty]
        public string UserName;

        [Parameter(Mandatory = false,
            HelpMessage = "Please enter a number to start at (default 0)")]
        public int Offset = 0;

        [Parameter(Mandatory = false,
            HelpMessage = "Please enter the total number of users to return (default 1000)")]
        public int Limit = 1000;

        static ServerCommandProxy _serverProxy;

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            if (Globals.authToken != null)
            {
                _serverProxy = new ServerCommandProxy(Globals.ComputerName, Globals.Port, Globals.authToken);
                try
                {
                    string[] pcutUserSharedAccounts = _serverProxy.ListUserSharedAccounts(UserName, Offset, Limit);
                    Collection<PSObject> returnPcutUserSharedAccounts = new Collection<PSObject>();
                    foreach (string pcutUserSharedAccount in pcutUserSharedAccounts)
                    {
                        PSObject thisSharedAccount = new PSObject();
                        thisSharedAccount.Properties.Add(new PSNoteProperty("Name", pcutUserSharedAccount));
                        returnPcutUserSharedAccounts.Add(thisSharedAccount);
                    }
                    WriteObject(returnPcutUserSharedAccounts);
                }
                catch (XmlRpcFaultException fex)
                {
                    ErrorRecord errRecord = new ErrorRecord(new Exception(fex.Message, fex.InnerException), fex.FaultString, ErrorCategory.NotSpecified, fex);
                    WriteError(errRecord);
                }
            }
            else
            {
                WriteObject("Please run Connect-PcutServer in order to establish connection.");
            }
        }
    }

    [Cmdlet(VerbsCommon.Get, "pcutUserAccountBalance")]
    public class Get_PcutUserAccountBalance : Cmdlet
    {
        [Parameter(Mandatory = true,
            HelpMessage = "Please provide the current username")]
        [ValidateNotNullOrEmpty]
        public string UserName;

        static ServerCommandProxy _serverProxy;

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            if (Globals.authToken != null)
            {
                _serverProxy = new ServerCommandProxy(Globals.ComputerName, Globals.Port, Globals.authToken);
                try
                {
                    double pcutUserAccountBalance = _serverProxy.GetUserAccountBalance(UserName,null);
                    PSObject returnPcutUserAccountBalance = new PSObject();
                    returnPcutUserAccountBalance.Properties.Add(new PSNoteProperty("Username",UserName));
                    returnPcutUserAccountBalance.Properties.Add(new PSNoteProperty("Balance",pcutUserAccountBalance));
                    WriteObject(returnPcutUserAccountBalance);
                }
                catch (XmlRpcFaultException fex)
                {
                    ErrorRecord errRecord = new ErrorRecord(new Exception(fex.Message, fex.InnerException), fex.FaultString, ErrorCategory.NotSpecified, fex);
                    WriteError(errRecord);
                }
            }
            else
            {
                WriteObject("Please run Connect-PcutServer in order to establish connection.");
            }
        }
    }

    [Cmdlet(VerbsCommon.Get, "pcutUserProperties")]
    public class Get_PcutUserProperties : Cmdlet
    {
        [Parameter(Mandatory = true,
            HelpMessage = "Please provide the current username")]
        [ValidateNotNullOrEmpty]
        public string UserName;

        static ServerCommandProxy _serverProxy;

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            if (Globals.authToken != null)
            {
                _serverProxy = new ServerCommandProxy(Globals.ComputerName, Globals.Port, Globals.authToken);
                try
                {
                    string[] propertyNames = new string[] { "full-name","email","disabled-print","balance","restricted","account-selection.mode","department","office","card-number","card-pin","notes" };
                    string[] pcutUserProperties = _serverProxy.GetUserProperties(UserName,propertyNames);
                    PSObject returnPcutUserProperties = new PSObject();
                    returnPcutUserProperties.Properties.Add(new PSNoteProperty("Username",UserName));
                    int propertyCount = 0;
                    foreach (string propertyName in propertyNames)
                    {
                        returnPcutUserProperties.Properties.Add(new PSNoteProperty(propertyName, pcutUserProperties[propertyCount]));
                        propertyCount += 1;
                    }
                    WriteObject(returnPcutUserProperties);
                }
                catch (XmlRpcFaultException fex)
                {
                    ErrorRecord errRecord = new ErrorRecord(new Exception(fex.Message, fex.InnerException), fex.FaultString, ErrorCategory.NotSpecified, fex);
                    WriteError(errRecord);
                }
            }
            else
            {
                WriteObject("Please run Connect-PcutServer in order to establish connection.");
            }
        }
    }

    [Cmdlet(VerbsCommon.Set, "pcutUserProperty")]
    public class Set_PcutUserProperty : Cmdlet
    {
        [Parameter(Mandatory = true,
            HelpMessage = "Please provide the current username")]
        [ValidateNotNullOrEmpty]
        public string UserName;

        [Parameter(Mandatory = true,
            HelpMessage = "Please enter a valid propertyName Valid options include: card-number, card-pin, department, email, full-name, notes, office,")]
        [ValidateSet(new string[] { "card-number","card-pin","department","email","full-name","notes","office" })]
        public string PropertyName;

        [Parameter(Mandatory = true,
            HelpMessage = "Please provide a propertyValue")]
        [ValidateNotNullOrEmpty]
        public string PropertyValue;

        static ServerCommandProxy _serverProxy;

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            if (Globals.authToken != null)
            {
                _serverProxy = new ServerCommandProxy(Globals.ComputerName, Globals.Port, Globals.authToken);
                try
                {
                    _serverProxy.SetUserProperty(UserName, PropertyName, PropertyValue);
                    PSObject returnSetUserProperty = new PSObject();
                    returnSetUserProperty.Properties.Add(new PSNoteProperty("Username", UserName));
                    returnSetUserProperty.Properties.Add(new PSNoteProperty("propertyName", PropertyName));
                    returnSetUserProperty.Properties.Add(new PSNoteProperty("propertyValue", PropertyValue));
                    WriteObject(returnSetUserProperty);
                }
                catch (XmlRpcFaultException fex)
                {
                    ErrorRecord errRecord = new ErrorRecord(new Exception(fex.Message, fex.InnerException), fex.FaultString, ErrorCategory.NotSpecified, fex);
                    WriteError(errRecord);
                }
            }
            else
            {
                WriteObject("Please run Connect-PcutServer in order to establish connection.");
            }
        }
    }

    [Cmdlet(VerbsCommon.Get, "pcutUserProperty")]
    public class Get_PcutUserProperty : Cmdlet
    {
        [Parameter(Mandatory = true,
            HelpMessage = "Please provide the current username")]
        [ValidateNotNullOrEmpty]
        public string UserName;

        [Parameter(Mandatory = true,
            HelpMessage = "Please provide a propertyName")]
        [ValidateNotNullOrEmpty]
        public string PropertyName;

        static ServerCommandProxy _serverProxy;

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            if (Globals.authToken != null)
            {
                _serverProxy = new ServerCommandProxy(Globals.ComputerName, Globals.Port, Globals.authToken);
                try
                {
                    string PropertyValue = _serverProxy.GetUserProperty(UserName, PropertyName);
                    PSObject returnPcutGetUserProperty = new PSObject();
                    returnPcutGetUserProperty.Properties.Add(new PSNoteProperty("Username", UserName));
                    returnPcutGetUserProperty.Properties.Add(new PSNoteProperty("propertyName", PropertyName));
                    returnPcutGetUserProperty.Properties.Add(new PSNoteProperty("propertyValue", PropertyValue));
                    WriteObject(returnPcutGetUserProperty);
                }
                catch (XmlRpcFaultException fex)
                {
                    ErrorRecord errRecord = new ErrorRecord(new Exception(fex.Message, fex.InnerException), fex.FaultString, ErrorCategory.NotSpecified, fex);
                    WriteError(errRecord);
                }
            }
            else
            {
                WriteObject("Please run Connect-PcutServer in order to establish connection.");
            }
        }
    }

    [Cmdlet(VerbsCommon.Set, "pcutUserAccountBalance")]
    public class Set_PcutUserAccountBalance : Cmdlet
    {
        [Parameter(Mandatory = true,
            HelpMessage = "Please provide the current username")]
        [ValidateNotNullOrEmpty]
        public string UserName;

        [Parameter(Mandatory = true,
            HelpMessage = "Enter the new balance")]
        [ValidateNotNullOrEmpty]
        public double Balance;

        [Parameter(Mandatory = false,
            HelpMessage = "Enter an optional comment")]
        public string Comment;

        static ServerCommandProxy _serverProxy;

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            if (Globals.authToken != null)
            {
                _serverProxy = new ServerCommandProxy(Globals.ComputerName, Globals.Port, Globals.authToken);
                try
                {
                    _serverProxy.SetUserAccountBalance(UserName, Balance, Comment, null);
                    PSObject returnPcutSetUserAccountBalance = new PSObject();
                    returnPcutSetUserAccountBalance.Properties.Add(new PSNoteProperty("Username",UserName));
                    returnPcutSetUserAccountBalance.Properties.Add(new PSNoteProperty("Balance",Balance));
                    returnPcutSetUserAccountBalance.Properties.Add(new PSNoteProperty("Comment", Comment));
                    WriteObject(returnPcutSetUserAccountBalance);
                }
                catch (XmlRpcFaultException fex)
                {
                    ErrorRecord errRecord = new ErrorRecord(new Exception(fex.Message, fex.InnerException), fex.FaultString, ErrorCategory.NotSpecified, fex);
                    WriteError(errRecord);
                }
            }
            else
            {
                WriteObject("Please run Connect-PcutServer in order to establish connection.");
            }
        }
    }

    [Cmdlet(VerbsCommon.Get, "pcutGroup")]
    public class Get_PcutGroup : Cmdlet
    {
        [Parameter(Mandatory = false,
            HelpMessage = "Please enter a number to start at (default 0)")]
        public int Offset = 0;

        [Parameter(Mandatory = false,
            HelpMessage = "Please enter the total number of users to return (default 1000)")]
        public int Limit = 1000;

        static ServerCommandProxy _serverProxy;

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            if (Globals.authToken != null)
            {
                _serverProxy = new ServerCommandProxy(Globals.ComputerName, Globals.Port, Globals.authToken);
                try
                {
                    string[] pcutGroups = _serverProxy.ListUserGroups(Offset, Limit);
                    Collection<PSObject> returnPcutGroups = new Collection<PSObject>();
                    foreach (string pcutGroup in pcutGroups)
                    {
                        PSObject thisGroup = new PSObject();
                        thisGroup.Properties.Add(new PSNoteProperty("Name",pcutGroup));
                        returnPcutGroups.Add(thisGroup);
                    }
                    WriteObject(returnPcutGroups);
                }
                catch (XmlRpcFaultException fex)
                {
                    ErrorRecord errRecord = new ErrorRecord(new Exception(fex.Message, fex.InnerException), fex.FaultString, ErrorCategory.NotSpecified, fex);
                    WriteError(errRecord);
                }
            }
            else
            {
                WriteObject("Please run Connect-PcutServer in order to establish connection.");
            }
        }
    }

    [Cmdlet(VerbsCommon.Get, "pcutUserGroup")]
    public class Get_PcutUserGroup : Cmdlet
    {
        [Parameter(Mandatory = true,
            HelpMessage = "Please provide the current username")]
        [ValidateNotNullOrEmpty]
        public string UserName;

        static ServerCommandProxy _serverProxy;

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            if (Globals.authToken != null)
            {
                _serverProxy = new ServerCommandProxy(Globals.ComputerName, Globals.Port, Globals.authToken);
                try
                {
                    string[] pcutUserGroups = _serverProxy.GetUserGroups(UserName);
                    Collection<PSObject> returnPcutUserGroups = new Collection<PSObject>();
                    foreach (string pcutUserGroup in pcutUserGroups)
                    {
                        PSObject thisGroup = new PSObject();
                        thisGroup.Properties.Add(new PSNoteProperty("Name", pcutUserGroup));
                        returnPcutUserGroups.Add(thisGroup);
                    }
                    WriteObject(returnPcutUserGroups);
                }
                catch (XmlRpcFaultException fex)
                {
                    ErrorRecord errRecord = new ErrorRecord(new Exception(fex.Message, fex.InnerException), fex.FaultString, ErrorCategory.NotSpecified, fex);
                    WriteError(errRecord);
                }
            }
            else
            {
                WriteObject("Please run Connect-PcutServer in order to establish connection.");
            }
        }
    }

    [Cmdlet(VerbsData.Update, "pcutUserAccountBalance")]
    public class Update_PcutUserAccountBalance : Cmdlet
    {
        [Parameter(Mandatory = true,
            HelpMessage = "Please provide the current username")]
        [ValidateNotNullOrEmpty]
        public string UserName;

        [Parameter(Mandatory = true,
            HelpMessage = "Enter a positive or negative number to adjust balance by")]
        [ValidateNotNullOrEmpty]
        public double Adjustment;

        [Parameter(Mandatory = false,
            HelpMessage = "Enter an optional comment")]
        public string Comment;

        static ServerCommandProxy _serverProxy;

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            if (Globals.authToken != null)
            {
                _serverProxy = new ServerCommandProxy(Globals.ComputerName, Globals.Port, Globals.authToken);
                try
                {
                    double pcutUserPreviousBalance = _serverProxy.GetUserAccountBalance(UserName, null);
                    _serverProxy.AdjustUserAccountBalance(UserName, Adjustment, Comment, null);
                    double pcutUserNewBalance = _serverProxy.GetUserAccountBalance(UserName, null);
                    PSObject returnUpdatePcutUserAccountBalance = new PSObject();
                    returnUpdatePcutUserAccountBalance.Properties.Add(new PSNoteProperty("Username", UserName));
                    returnUpdatePcutUserAccountBalance.Properties.Add(new PSNoteProperty("OldBalance", pcutUserPreviousBalance));
                    returnUpdatePcutUserAccountBalance.Properties.Add(new PSNoteProperty("Adjustment", Adjustment));
                    returnUpdatePcutUserAccountBalance.Properties.Add(new PSNoteProperty("Balance", pcutUserNewBalance));
                    WriteObject(returnUpdatePcutUserAccountBalance);
                }
                catch (XmlRpcFaultException fex)
                {
                    ErrorRecord errRecord = new ErrorRecord(new Exception(fex.Message, fex.InnerException), fex.FaultString, ErrorCategory.NotSpecified, fex);
                    WriteError(errRecord);
                }
            }
            else
            {
                WriteObject("Please run Connect-PcutServer in order to establish connection.");
            }
        }
    }

    [Cmdlet(VerbsData.Update, "pcutGroupAccountBalance")]
    public class Update_PcutGroupAccountBalance : Cmdlet
    {
        [Parameter(Mandatory = true,
            HelpMessage = "Please provide the name of the group")]
        [ValidateNotNullOrEmpty]
        public string GroupName;

        [Parameter(Mandatory = true,
            HelpMessage = "Enter a positive or negative number to adjust balance by")]
        [ValidateNotNullOrEmpty]
        public double Adjustment;

        [Parameter(Mandatory = false,
            HelpMessage = "Enter an optional comment")]
        public string Comment;

        static ServerCommandProxy _serverProxy;

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            if (Globals.authToken != null)
            {
                _serverProxy = new ServerCommandProxy(Globals.ComputerName, Globals.Port, Globals.authToken);
                try
                {
                    _serverProxy.AdjustUserAccountBalanceByGroup(GroupName, Adjustment, Comment, null);
                    PSObject returnUpdatePcutGroupAccountBalance = new PSObject();
                    returnUpdatePcutGroupAccountBalance.Properties.Add(new PSNoteProperty("Name", GroupName));
                    returnUpdatePcutGroupAccountBalance.Properties.Add(new PSNoteProperty("Adjustment", Adjustment));
                    returnUpdatePcutGroupAccountBalance.Properties.Add(new PSNoteProperty("Comment", Comment));
                    WriteObject(returnUpdatePcutGroupAccountBalance);
                }
                catch (XmlRpcFaultException fex)
                {
                    ErrorRecord errRecord = new ErrorRecord(new Exception(fex.Message, fex.InnerException), fex.FaultString, ErrorCategory.NotSpecified, fex);
                    WriteError(errRecord);
                }
            }
            else
            {
                WriteObject("Please run Connect-PcutServer in order to establish connection.");
            }
        }
    }

    [Cmdlet(VerbsCommon.Reset, "pcutUserCounts")]
    public class Reset_PcutUserCounts : Cmdlet
    {
        [Parameter(Mandatory = true,
            HelpMessage = "Please provide the current username")]
        [ValidateNotNullOrEmpty]
        public string UserName;

        [Parameter(Mandatory = true,
            HelpMessage = "Please provide the username who reset the counts")]
        [ValidateNotNullOrEmpty]
        public string ResetBy;

        static ServerCommandProxy _serverProxy;

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            if (Globals.authToken != null)
            {
                _serverProxy = new ServerCommandProxy(Globals.ComputerName, Globals.Port, Globals.authToken);
                try
                {
                    _serverProxy.ResetUserCounts(UserName, ResetBy);
                    WriteObject("Counts for user " + UserName + " reset by " + ResetBy);
                }
                catch (XmlRpcFaultException fex)
                {
                    ErrorRecord errRecord = new ErrorRecord(new Exception(fex.Message, fex.InnerException), fex.FaultString, ErrorCategory.NotSpecified, fex);
                    WriteError(errRecord);
                }
            }
            else
            {
                WriteObject("Please run Connect-PcutServer in order to establish connection.");
            }
        }
    }

    [Cmdlet(VerbsCommon.Reset, "pcutInitialUserSettings")]
    public class Reset_PcutInitialUserSettings : Cmdlet
    {
        [Parameter(Mandatory = true,
            HelpMessage = "Please provide the current username")]
        [ValidateNotNullOrEmpty]
        public string UserName;

        static ServerCommandProxy _serverProxy;

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            if (Globals.authToken != null)
            {
                _serverProxy = new ServerCommandProxy(Globals.ComputerName, Globals.Port, Globals.authToken);
                try
                {
                    _serverProxy.ReapplyInitialUserSettings(UserName);
                    WriteObject(UserName + " account reset");
                }
                catch (XmlRpcFaultException fex)
                {
                    ErrorRecord errRecord = new ErrorRecord(new Exception(fex.Message, fex.InnerException), fex.FaultString, ErrorCategory.NotSpecified, fex);
                    WriteError(errRecord);
                }
            }
            else
            {
                WriteObject("Please run Connect-PcutServer in order to establish connection.");
            }
        }
    }

    [Cmdlet(VerbsCommon.Rename, "pcutUser")]
    public class Rename_PcutUser : Cmdlet
    {
        [Parameter(Mandatory = true,
            HelpMessage = "Please provide the current username")]
        [ValidateNotNullOrEmpty]
        public string currentUserName;

        [Parameter(Mandatory = true,
            HelpMessage = "Please provide a new user name")]
        [ValidateNotNullOrEmpty]
        public string newUserName;

        static ServerCommandProxy _serverProxy;

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            if (Globals.authToken != null)
            {
                _serverProxy = new ServerCommandProxy(Globals.ComputerName, Globals.Port, Globals.authToken);
                try
                {
                    _serverProxy.RenameUserAccount(currentUserName, newUserName);
                    if (_serverProxy.UserExists(newUserName))
                    {
                        PSObject objNewUser = new PSObject();
                        PSNoteProperty noteProp = new PSNoteProperty("NewUsername", newUserName);
                        objNewUser.Properties.Add(noteProp);
                        WriteObject(objNewUser);
                    }
                }
                catch (XmlRpcFaultException fex)
                {
                    ErrorRecord errRecord = new ErrorRecord(new Exception(fex.Message, fex.InnerException), fex.FaultString, ErrorCategory.NotSpecified, fex);
                    WriteError(errRecord);
                }
            }
            else
            {
                WriteObject("Please run Connect-PcutServer in order to establish connection.");
            }
        }
    }

}
