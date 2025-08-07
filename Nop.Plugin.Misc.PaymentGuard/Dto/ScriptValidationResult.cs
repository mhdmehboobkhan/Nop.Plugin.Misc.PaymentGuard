namespace Nop.Plugin.Misc.PaymentGuard.Dto
{
    public class ScriptValidationResult
    {
        public string ScriptUrl { get; set; }
        public bool IsAuthorized { get; set; }
        public bool HasValidSRI { get; set; }
        public SRIValidationResult SRIValidation { get; set; }
        public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
    }
}