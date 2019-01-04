using Sitecore.Modules.EmailCampaign.Core;
using System;
using System.Web.UI;

namespace Sitecore.Support.Modules.EmailCampaign.UI
{
  public class RedirectUrlPage : Page
  {
    protected override void OnLoad(EventArgs e)
    {
      base.Response.Status = "301 Moved Permanently";
      base.Response.AddHeader("Location", Sitecore.Support.Modules.EmailCampaign.Core.EmailResponse.HandleEmailLinkClickResponse());
    }
  }
}
