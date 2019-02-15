using Sitecore;
using Sitecore.Analytics;
using Sitecore.Analytics.Data.DataAccess.DataSets;
using Sitecore.Analytics.Data.Items;
using Sitecore.Analytics.Extensions;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Modules.EmailCampaign;
using Sitecore.Modules.EmailCampaign.Core;
using Sitecore.Modules.EmailCampaign.Core.Analytics;
using Sitecore.Modules.EmailCampaign.Core.Extensions;
using Sitecore.Modules.EmailCampaign.Exceptions;
using Sitecore.Modules.EmailCampaign.Messages;
using Sitecore.Text;
using System;
using Factory = Sitecore.Modules.EmailCampaign.Factory;

namespace Sitecore.Support.Modules.EmailCampaign.Core
{


  public class EmailResponse
  {
    private static string GetMessageId(string campaignData)
    {
      string str;
      if (campaignData.Length == 0x4c)
      {
        str = campaignData.Substring(0x26);
      }
      else
      {
        str = (campaignData.Length != 0x26) ? string.Empty : campaignData;
      }
      return str;
    }

    public static string HandleEmailLinkClickResponse()
    {
      try
      {
        string str = Context.Request.QueryString["ec_url"];
        if (string.IsNullOrEmpty(str))
        {
          throw new EmailCampaignException("The 'ec_url' query string parameter is null or empty.");
        }
        string str2 = Context.Request.QueryString[GlobalSettings.CampaignQueryStringKey];
        if (!(!string.IsNullOrEmpty(str2) && ShortID.IsShortID(str2)))
        {
          throw new EmailCampaignException("The '" + GlobalSettings.CampaignQueryStringKey + "' query string parameter is null, empty or wrong.");
        }
        string str3 = Context.Request.QueryString[GlobalSettings.AutomationStateQueryKey];
        if (!(!string.IsNullOrEmpty(str3) && ShortID.IsShortID(str3)))
        {
          throw new EmailCampaignException("The '" + GlobalSettings.AutomationStateQueryKey + "' query string parameter is null, empty or wrong.");
        }
        return HandleEmailLinkClickResponse(str, new Guid(str2), new Guid(str3));
      }
      catch (Exception exception1)
      {
        Logging.LogError(exception1);
        Tracker.CurrentPage.Cancel();
        return Settings.ItemNotFoundUrl;
      }
    }

    private static string HandleEmailLinkClickResponse(string link, Guid campaignId, Guid automationStateId)
    {
      string str2;
      bool flag = link.StartsWith("/", StringComparison.OrdinalIgnoreCase) && !link.StartsWith("//", StringComparison.OrdinalIgnoreCase);
      if ((campaignId == Guid.Empty) || (automationStateId == Guid.Empty))
      {
        Tracker.CurrentPage.Cancel();
        str2 = (!flag && !Context.User.IsEcmUser()) ? Settings.ItemNotFoundUrl : link;
      }
      else
      {
        CampaignItem contentDbItem = (CampaignItem)ItemUtilExt.GetContentDbItem(new ID(campaignId));
        if (ReferenceEquals(contentDbItem, null))
        {
          throw new EmailCampaignException($"Campaign with ID {campaignId} has not been found.");
        }
        MessageItem message = Factory.GetMessage(GetMessageId(contentDbItem.Data));
        if (ReferenceEquals(message, null))
        {
          throw new EmailCampaignException($"Message for the '{contentDbItem.ID}' campaign has not been found.");
        }
        if (!flag)
        {
          //flag = link.StartsWith(message.ManagerRoot.Settings.BaseURL);

          //Fix Sitecore.Support.301981: strict checking against host
          try
          {
            var linkUri = new Uri(link);
            var baseUri = new Uri(message.ManagerRoot.Settings.BaseURL);
            flag = linkUri.Scheme == baseUri.Scheme && linkUri.Host == baseUri.Host;
          }
          catch (UriFormatException e)
          {
            //not expected but ignore if happens
          }        
          //End fix Sitecore.Support.301981
        }
        if (!(flag || Sitecore.Support.Modules.EmailCampaign.Core.ExternalLinks.IsLinkRegistered(message, link)))
        {
          throw new EmailCampaignException("The unregistered external link: " + link + " detected.");
        }
        if (Tracker.Visitor.VisitorClassification >= 900)
        {
          Tracker.Visitor.SetVisitorClassification(0, 0, false);
        }
        VisitorDataSet.AutomationStatesRow automationState = AnalyticsHelper.GetAutomationState(automationStateId);
        if (!ReferenceEquals(automationState, null))
        {
          UpdateAutomationStateObject(automationState);
          UpdateVisitorInfo(automationState.UserName);
        }
        VisitorDataSet.PagesRow page = flag ? Tracker.CurrentVisit.GetOrCreateNextPage() : Tracker.CurrentPage;
        page.TriggerCampaign(contentDbItem);
        if (Tracker.CurrentPage.VisitPageIndex < 2)
        {
          if (Tracker.CurrentVisit.TrafficType == 0)
          {
            Tracker.CurrentVisit.TrafficType = 50;
          }
          if (!ReferenceEquals(automationState, null))
          {
            if (!(automationState.IsTestSetIdNull() || automationState.IsTestValuesNull()))
            {
              Tracker.CurrentVisit.TestSetId = automationState.TestSetId;
              Tracker.CurrentVisit.TestValues = automationState.TestValues;
            }
            SetVisitLanguage(automationState);
          }
        }
        if (flag)
        {
          Tracker.CurrentPage.Cancel();
        }
        page.Register("Click Email Link", automationStateId.ToString());
        if (flag)
        {
          UrlString str = new UrlString(link);
          str.Add(GlobalSettings.AutomationStateQueryKey, new ShortID(automationStateId).ToString());
          link = str.ToString();
        }
        str2 = link.Replace("&amp;", "&");
      }
      return str2;
    }

    public static void HandleEmailOpenResponse()
    {
      string str = Context.Request.QueryString[GlobalSettings.AutomationStateQueryKey];
      if (((!string.IsNullOrEmpty(str) && ShortID.IsShortID(str)) && string.IsNullOrEmpty(Context.Request.QueryString[GlobalSettings.EcmIdQueryStringKey])) && (Context.Request.FilePath.IndexOf("RegisterEmailOpened", StringComparison.OrdinalIgnoreCase) != -1))
      {
        try
        {
          AnalyticsHelper.HandleEmailOpenResponse(new Guid(str));
        }
        catch (Exception exception1)
        {
          Logging.LogError(exception1);
        }
      }
    }

    private static void SetVisitLanguage(VisitorDataSet.AutomationStatesRow automationStateObject)
    {
      string data = automationStateObject.Data;
      if (!string.IsNullOrEmpty(data))
      {
        int index = data.IndexOf("lang=", StringComparison.OrdinalIgnoreCase);
        if ((index > -1) && (data.Length > (index + 10)))
        {
          Tracker.CurrentVisit.Language = data.Substring(index + 5, 5).TrimEnd(new char[0]);
        }
      }
    }

    private static void UpdateAutomationStateObject(VisitorDataSet.AutomationStatesRow obj)
    {
      Item contentDbItem = ItemUtilExt.GetContentDbItem(new ID(obj.StateId));
      if ((!ReferenceEquals(contentDbItem, null) && !string.IsNullOrEmpty(contentDbItem.Name)) && !ReferenceEquals(contentDbItem.Parent, null))
      {
        Item objA = contentDbItem.Parent.Children["Clicked Through"];
        if (!ReferenceEquals(objA, null) && !string.IsNullOrEmpty(objA.Name))
        {
          bool num1;
          if (ValidateState(contentDbItem) && ValidateState(objA))
          {
            obj.StateId = objA.ID.ToGuid();
            obj.StateName = "Clicked Through";
          }
          if (obj.IsVisitorIdNull() || (obj.VisitorId == Guid.Empty))
          {
            num1 = (obj.UserName == Tracker.Visitor.ExternalUser) ? false : !string.IsNullOrEmpty(Tracker.Visitor.ExternalUser);
          }
          else
          {
            num1 = false;
          }
          if (!num1)
          {
            obj.VisitorId = Tracker.Visitor.VisitorId;
          }
        }
      }
    }

    private static Contact UpdateVisitorInfo(string userName)
    {
      Contact contactFromName = Factory.GetContactFromName(userName);
      if (!ReferenceEquals(contactFromName, null))
      {
        if (contactFromName.Profile.UndeliveredCount > 0)
        {
          contactFromName.Profile.UndeliveredCount = 0;
          contactFromName.Profile.Save();
        }
        Tracker.Visitor.Tags["email"] = contactFromName.Profile.Email;
        if (string.IsNullOrEmpty(Tracker.Visitor.ExternalUser))
        {
          Tracker.Visitor.ExternalUser = contactFromName.Name;
        }
      }
      else
      {
        if (!((Tracker.Visitor.Tags["email"] != null) && Tracker.Visitor.Tags["email"].Contains("@")))
        {
          int index = userName.IndexOf('\\');
          if ((index >= 0) && userName.Contains("@"))
          {
            Tracker.Visitor.Tags["email"] = userName.Substring(index);
          }
        }
        if (string.IsNullOrEmpty(Tracker.Visitor.ExternalUser))
        {
          Tracker.Visitor.ExternalUser = userName;
        }
      }
      return contactFromName;
    }

    private static bool ValidateState(Item item)
    {
      bool num1;
      if ((string.Equals(item.Name, "Message Opened", StringComparison.OrdinalIgnoreCase) || (string.Equals(item.Name, "Clicked Through", StringComparison.OrdinalIgnoreCase) || (string.Equals(item.Name, "Send Completed", StringComparison.OrdinalIgnoreCase) || (string.Equals(item.Name, "Hard Bounce", StringComparison.OrdinalIgnoreCase) || (string.Equals(item.Name, "Invalid Address", StringComparison.OrdinalIgnoreCase) || string.Equals(item.Name, "Recipient Queued", StringComparison.OrdinalIgnoreCase)))))) || string.Equals(item.Name, "Send in Progress", StringComparison.OrdinalIgnoreCase))
      {
        num1 = true;
      }
      else
      {
        num1 = string.Equals(item.Name, "Soft Bounce", StringComparison.OrdinalIgnoreCase);
      }
      return (bool)num1;
    }
  }
}
