namespace Nop.Plugin.Misc.PaymentGuard.Dto
{
    public class ValidateScriptWithSRIRequest
    {
        public string ScriptUrl { get; set; }
        public string Integrity { get; set; }
        public string PageUrl { get; set; }
    }
}