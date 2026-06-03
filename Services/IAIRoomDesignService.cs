using ARInteriorDesignApp.Models;

namespace ARInteriorDesignApp.Services
{
    public interface IAIRoomDesignService
    {
        Task<AIRoomDesignResult> GenerateFromTextAsync(AIRoomDesignViewModel model);

        Task<AIRoomDesignResult> GenerateFromImageAsync(AIRoomDesignViewModel model, string imagePath);

        Task<AIRoomDesignResult> RegenerateWithChangesAsync(AIRoomDesignViewModel model);
    }
}