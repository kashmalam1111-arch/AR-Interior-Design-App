namespace ARInteriorDesignApp.Models
{
    public class AIRoomDesignResult
    {
        public string PromptUsed { get; set; } = string.Empty;

        public string GeneratedImageUrl { get; set; } = string.Empty;

        public string SuggestionText { get; set; } = string.Empty;

        public bool Success { get; set; } = false;

        public string ErrorMessage { get; set; } = string.Empty;
    }
}