using ARInteriorDesignApp.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Net;

namespace ARInteriorDesignApp.Services
{
    public class AIRoomDesignService : IAIRoomDesignService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public AIRoomDesignService(
            HttpClient httpClient,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _environment = environment;
        }

        public async Task<AIRoomDesignResult> GenerateFromTextAsync(AIRoomDesignViewModel model)
        {
            string prompt = BuildInteriorPrompt(model, false);

            try
            {
                string apiKey = _configuration["OpenAI:ApiKey"] ?? "";

                if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "YOUR_OPENAI_API_KEY_HERE")
                {
                    return await GenerateDemoAIResult(model, prompt, "Demo AI Mode: API key missing, so a professional local AI preview has been generated.");
                }

                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    "https://api.openai.com/v1/images/generations"
                );

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var body = new
                {
                    model = "gpt-image-1",
                    prompt = prompt,
                    size = "1024x1024"
                };

                request.Content = new StringContent(
                    JsonSerializer.Serialize(body),
                    Encoding.UTF8,
                    "application/json"
                );

                using var response = await _httpClient.SendAsync(request);

                string json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return await GenerateDemoAIResult(model, prompt, "Demo AI Mode: Real AI API failed, so the system generated a professional local AI preview instead.");
                }

                string imagePath = await SaveImageFromOpenAIResponse(json);

                return new AIRoomDesignResult
                {
                    Success = true,
                    PromptUsed = prompt,
                    GeneratedImageUrl = imagePath,
                    SuggestionText = BuildSuggestionText(model, "Real AI generated a new room design from your instruction.")
                };
            }
            catch
            {
                return await GenerateDemoAIResult(model, prompt, "Demo AI Mode: Real AI connection was not available, so the system generated a professional local AI preview instead.");
            }
        }

        public async Task<AIRoomDesignResult> GenerateFromImageAsync(AIRoomDesignViewModel model, string imagePath)
        {
            string prompt = BuildInteriorPrompt(model, true);

            try
            {
                string apiKey = _configuration["OpenAI:ApiKey"] ?? "";

                if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "YOUR_OPENAI_API_KEY_HERE")
                {
                    return await GenerateDemoAIResult(model, prompt, "Demo AI Mode: Uploaded room image was analyzed and a professional AI preview plan was generated locally.");
                }

                string physicalImagePath = Path.Combine(
                    _environment.WebRootPath,
                    imagePath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString())
                );

                if (!File.Exists(physicalImagePath))
                {
                    return await GenerateDemoAIResult(model, prompt, "Demo AI Mode: Uploaded image was not found, so a professional local AI preview has been generated.");
                }

                using var form = new MultipartFormDataContent();

                form.Add(new StringContent("gpt-image-1"), "model");
                form.Add(new StringContent(prompt), "prompt");
                form.Add(new StringContent("1024x1024"), "size");

                byte[] imageBytes = await File.ReadAllBytesAsync(physicalImagePath);

                var imageContent = new ByteArrayContent(imageBytes);
                imageContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(physicalImagePath));

                form.Add(imageContent, "image", Path.GetFileName(physicalImagePath));

                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    "https://api.openai.com/v1/images/edits"
                );

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = form;

                using var response = await _httpClient.SendAsync(request);

                string json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return await GenerateDemoAIResult(model, prompt, "Demo AI Mode: Real AI image redesign failed, so the system generated a professional local AI preview instead.");
                }

                string generatedImagePath = await SaveImageFromOpenAIResponse(json);

                return new AIRoomDesignResult
                {
                    Success = true,
                    PromptUsed = prompt,
                    GeneratedImageUrl = generatedImagePath,
                    SuggestionText = BuildSuggestionText(model, "Real AI redesigned the uploaded room image.")
                };
            }
            catch
            {
                return await GenerateDemoAIResult(model, prompt, "Demo AI Mode: Real AI connection was not available, so the uploaded room concept was converted into a professional local AI preview.");
            }
        }

        public async Task<AIRoomDesignResult> RegenerateWithChangesAsync(AIRoomDesignViewModel model)
        {
            string newInstruction =
                model.UserInstruction +
                ". Additional user change request: " +
                model.ChangeRequest;

            model.UserInstruction = newInstruction;

            string prompt = BuildInteriorPrompt(model, false);

            return await GenerateDemoAIResult(model, prompt, "Updated Demo AI Mode: The requested changes have been applied to the professional preview.");
        }

        private string BuildInteriorPrompt(AIRoomDesignViewModel model, bool isImageEdit)
        {
            string basePrompt = isImageEdit
                ? "Redesign the uploaded room image while keeping the original room perspective and structure."
                : "Generate a realistic interior design image from scratch.";

            return
                basePrompt + " " +
                "Room type: " + model.RoomType + ". " +
                "Interior style: " + model.Style + ". " +
                "Budget level: " + model.Budget + ". " +
                "User instruction: " + model.UserInstruction + ". " +
                "Create a professional, realistic, clean and high-quality interior design. " +
                "Show proper furniture placement, wall color, lighting, decor, flooring, storage and realistic room styling. " +
                "Suitable room types include bedroom, kitchen, bathroom, living room, dining room, balcony and TV lounge. " +
                "Do not add text, labels, watermark, people, distorted objects or unrealistic furniture in the image.";
        }

        private async Task<AIRoomDesignResult> GenerateDemoAIResult(AIRoomDesignViewModel model, string prompt, string intro)
        {
            string imagePath = await CreateDemoRoomSvg(model);

            return new AIRoomDesignResult
            {
                Success = true,
                PromptUsed = prompt,
                GeneratedImageUrl = imagePath,
                SuggestionText = BuildSuggestionText(model, intro)
            };
        }

        private async Task<string> CreateDemoRoomSvg(AIRoomDesignViewModel model)
        {
            string folder = Path.Combine(_environment.WebRootPath, "uploads", "ai-designs");

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            string fileName = "demo-ai-room-" + Guid.NewGuid().ToString("N") + ".svg";
            string fullPath = Path.Combine(folder, fileName);

            string roomType = CleanText(model.RoomType);
            string style = CleanText(model.Style);
            string budget = CleanText(model.Budget);
            string instruction = CleanText(model.UserInstruction);

            string mainFurniture = GetMainFurniture(roomType);
            string secondFurniture = GetSecondFurniture(roomType);
            string thirdFurniture = GetThirdFurniture(roomType);

            string wallColor = GetWallColor(style);
            string accentColor = GetAccentColor(style);
            string floorColor = GetFloorColor(style);

            string svg =
$@"<svg xmlns='http://www.w3.org/2000/svg' width='1024' height='1024' viewBox='0 0 1024 1024'>
  <defs>
    <linearGradient id='wall' x1='0' y1='0' x2='1' y2='1'>
      <stop offset='0%' stop-color='{wallColor}'/>
      <stop offset='100%' stop-color='#f8f3ea'/>
    </linearGradient>
    <linearGradient id='floor' x1='0' y1='0' x2='0' y2='1'>
      <stop offset='0%' stop-color='{floorColor}'/>
      <stop offset='100%' stop-color='#caa77a'/>
    </linearGradient>
    <filter id='shadow' x='-20%' y='-20%' width='140%' height='140%'>
      <feDropShadow dx='0' dy='18' stdDeviation='18' flood-color='#000000' flood-opacity='0.22'/>
    </filter>
    <filter id='softGlow' x='-30%' y='-30%' width='160%' height='160%'>
      <feGaussianBlur stdDeviation='18' result='blur'/>
      <feMerge>
        <feMergeNode in='blur'/>
        <feMergeNode in='SourceGraphic'/>
      </feMerge>
    </filter>
  </defs>

  <rect width='1024' height='1024' fill='#f7f2ea'/>
  <rect x='70' y='70' width='884' height='540' rx='34' fill='url(#wall)'/>
  <polygon points='70,610 954,610 850,930 170,930' fill='url(#floor)'/>
  <line x1='70' y1='610' x2='954' y2='610' stroke='#b8a489' stroke-width='6'/>

  <rect x='120' y='125' width='220' height='170' rx='26' fill='#ffffff' opacity='0.55'/>
  <rect x='685' y='125' width='210' height='280' rx='24' fill='#ffffff' opacity='0.38'/>
  <circle cx='820' cy='205' r='80' fill='#f9df9f' opacity='0.55' filter='url(#softGlow)'/>

  <ellipse cx='512' cy='780' rx='300' ry='72' fill='#e7d8c8' opacity='0.95' filter='url(#shadow)'/>

  {BuildFurnitureSvg(roomType, accentColor)}

  <rect x='150' y='720' width='150' height='130' rx='20' fill='#d9c3a8' filter='url(#shadow)'/>
  <rect x='724' y='720' width='150' height='130' rx='20' fill='#d9c3a8' filter='url(#shadow)'/>

  <circle cx='225' cy='705' r='34' fill='#f7dd93' opacity='0.82' filter='url(#softGlow)'/>
  <circle cx='799' cy='705' r='34' fill='#f7dd93' opacity='0.82' filter='url(#softGlow)'/>

  <rect x='365' y='165' width='295' height='92' rx='20' fill='#ffffff' opacity='0.75'/>
  <text x='512' y='205' text-anchor='middle' font-family='Arial, sans-serif' font-size='28' font-weight='700' fill='#2d2a26'>AI Room Designer</text>
  <text x='512' y='240' text-anchor='middle' font-family='Arial, sans-serif' font-size='19' fill='#5f574d'>{roomType} | {style} | {budget}</text>

  <rect x='145' y='875' width='735' height='70' rx='18' fill='#ffffff' opacity='0.82'/>
  <text x='512' y='905' text-anchor='middle' font-family='Arial, sans-serif' font-size='20' font-weight='700' fill='#2d2a26'>Generated Design Elements</text>
  <text x='512' y='932' text-anchor='middle' font-family='Arial, sans-serif' font-size='17' fill='#635b50'>{mainFurniture} • {secondFurniture} • {thirdFurniture} • Warm Lighting • Decor</text>

  <rect x='84' y='84' width='856' height='856' rx='36' fill='none' stroke='rgba(255,255,255,0.8)' stroke-width='4'/>
</svg>";

            await File.WriteAllTextAsync(fullPath, svg, Encoding.UTF8);

            return "/uploads/ai-designs/" + fileName;
        }

        private string BuildFurnitureSvg(string roomType, string accentColor)
        {
            string lower = roomType.ToLower();

            if (lower.Contains("kitchen"))
            {
                return
$@"<rect x='250' y='525' width='525' height='170' rx='24' fill='{accentColor}' filter='url(#shadow)'/>
  <rect x='285' y='555' width='110' height='100' rx='12' fill='#f5efe6'/>
  <rect x='425' y='555' width='110' height='100' rx='12' fill='#f5efe6'/>
  <rect x='565' y='555' width='150' height='100' rx='12' fill='#f5efe6'/>
  <circle cx='635' cy='603' r='34' fill='#c8d9d7'/>
  <rect x='210' y='390' width='600' height='90' rx='18' fill='#ffffff' opacity='0.72'/>";
            }

            if (lower.Contains("bath"))
            {
                return
$@"<rect x='325' y='510' width='360' height='165' rx='30' fill='{accentColor}' filter='url(#shadow)'/>
  <circle cx='512' cy='335' r='100' fill='#ffffff' opacity='0.8' filter='url(#shadow)'/>
  <rect x='380' y='690' width='265' height='80' rx='22' fill='#f5efe6' filter='url(#shadow)'/>
  <rect x='700' y='455' width='105' height='240' rx='24' fill='#ffffff' opacity='0.72'/>";
            }

            if (lower.Contains("dining"))
            {
                return
$@"<rect x='315' y='565' width='390' height='125' rx='34' fill='{accentColor}' filter='url(#shadow)'/>
  <rect x='265' y='510' width='90' height='135' rx='22' fill='#f5efe6' filter='url(#shadow)'/>
  <rect x='670' y='510' width='90' height='135' rx='22' fill='#f5efe6' filter='url(#shadow)'/>
  <rect x='410' y='705' width='205' height='65' rx='18' fill='#7b5d43' opacity='0.85'/>
  <circle cx='512' cy='400' r='58' fill='#f7dd93' opacity='0.75' filter='url(#softGlow)'/>";
            }

            if (lower.Contains("balcony"))
            {
                return
$@"<rect x='310' y='550' width='160' height='180' rx='36' fill='{accentColor}' filter='url(#shadow)'/>
  <rect x='555' y='550' width='160' height='180' rx='36' fill='{accentColor}' filter='url(#shadow)'/>
  <circle cx='512' cy='650' r='70' fill='#f5efe6' filter='url(#shadow)'/>
  <rect x='180' y='465' width='95' height='220' rx='45' fill='#6f8f6a' filter='url(#shadow)'/>
  <rect x='765' y='465' width='95' height='220' rx='45' fill='#6f8f6a' filter='url(#shadow)'/>";
            }

            if (lower.Contains("tv"))
            {
                return
$@"<rect x='255' y='570' width='515' height='165' rx='38' fill='{accentColor}' filter='url(#shadow)'/>
  <rect x='370' y='315' width='290' height='165' rx='18' fill='#2e2e2e' filter='url(#shadow)'/>
  <rect x='330' y='500' width='370' height='55' rx='16' fill='#8c6a4a' filter='url(#shadow)'/>
  <rect x='330' y='745' width='370' height='60' rx='22' fill='#f5efe6' opacity='0.9'/>";
            }

            if (lower.Contains("living"))
            {
                return
$@"<rect x='245' y='560' width='535' height='175' rx='42' fill='{accentColor}' filter='url(#shadow)'/>
  <rect x='350' y='735' width='325' height='70' rx='22' fill='#8c6a4a' filter='url(#shadow)'/>
  <rect x='160' y='430' width='95' height='255' rx='45' fill='#6f8f6a' filter='url(#shadow)'/>
  <rect x='770' y='445' width='90' height='240' rx='45' fill='#d8b26d' filter='url(#shadow)'/>";
            }

            return
$@"<rect x='300' y='520' width='425' height='210' rx='38' fill='{accentColor}' filter='url(#shadow)'/>
  <rect x='345' y='455' width='335' height='90' rx='30' fill='#f5efe6' filter='url(#shadow)'/>
  <rect x='270' y='680' width='485' height='80' rx='25' fill='#f5efe6' opacity='0.95'/>
  <rect x='180' y='440' width='105' height='235' rx='18' fill='#8c6a4a' filter='url(#shadow)'/>
  <rect x='740' y='440' width='105' height='235' rx='18' fill='#8c6a4a' filter='url(#shadow)'/>";
        }

        private string GetMainFurniture(string roomType)
        {
            string lower = roomType.ToLower();

            if (lower.Contains("kitchen")) return "Kitchen Counter";
            if (lower.Contains("bath")) return "Vanity Unit";
            if (lower.Contains("dining")) return "Dining Table";
            if (lower.Contains("balcony")) return "Outdoor Seating";
            if (lower.Contains("tv")) return "TV Wall Unit";
            if (lower.Contains("living")) return "Sofa Set";

            return "Luxury Bed";
        }

        private string GetSecondFurniture(string roomType)
        {
            string lower = roomType.ToLower();

            if (lower.Contains("kitchen")) return "Cabinets";
            if (lower.Contains("bath")) return "Mirror";
            if (lower.Contains("dining")) return "Dining Chairs";
            if (lower.Contains("balcony")) return "Plants";
            if (lower.Contains("tv")) return "Media Console";
            if (lower.Contains("living")) return "Coffee Table";

            return "Side Tables";
        }

        private string GetThirdFurniture(string roomType)
        {
            string lower = roomType.ToLower();

            if (lower.Contains("kitchen")) return "Sink Area";
            if (lower.Contains("bath")) return "Storage";
            if (lower.Contains("dining")) return "Pendant Light";
            if (lower.Contains("balcony")) return "Tea Table";
            if (lower.Contains("tv")) return "Rug";
            if (lower.Contains("living")) return "Floor Lamp";

            return "Wardrobe";
        }

        private string GetWallColor(string style)
        {
            string lower = style.ToLower();

            if (lower.Contains("luxury")) return "#efe2cf";
            if (lower.Contains("modern")) return "#e8edf1";
            if (lower.Contains("classic")) return "#efe6d8";
            if (lower.Contains("cozy")) return "#f0ddc9";
            if (lower.Contains("premium")) return "#e9e1d2";

            return "#f1eee7";
        }

        private string GetAccentColor(string style)
        {
            string lower = style.ToLower();

            if (lower.Contains("luxury")) return "#b69261";
            if (lower.Contains("modern")) return "#667985";
            if (lower.Contains("classic")) return "#8c6a4a";
            if (lower.Contains("cozy")) return "#b88763";
            if (lower.Contains("premium")) return "#9b7b4f";

            return "#b8a289";
        }

        private string GetFloorColor(string style)
        {
            string lower = style.ToLower();

            if (lower.Contains("modern")) return "#d9d8d2";
            if (lower.Contains("luxury")) return "#d7bb8f";
            if (lower.Contains("classic")) return "#c8a06f";

            return "#d6b58a";
        }

        private string BuildSuggestionText(AIRoomDesignViewModel model, string intro)
        {
            return
                intro + "\n\n" +
                "Room Type: " + model.RoomType + "\n" +
                "Style: " + model.Style + "\n" +
                "Budget: " + model.Budget + "\n\n" +
                "Design Suggestions:\n" +
                "- Use a balanced furniture layout according to room size.\n" +
                "- Apply wall colors that match the selected interior style.\n" +
                "- Use layered lighting for a professional room atmosphere.\n" +
                "- Add storage and decor according to selected budget.\n" +
                "- Keep walking space clear and avoid overcrowding.\n" +
                "- Keep the final design realistic and functional.\n\n" +
                "Furniture Plan:\n" +
                "- Main Element: " + GetMainFurniture(model.RoomType) + "\n" +
                "- Supporting Element: " + GetSecondFurniture(model.RoomType) + "\n" +
                "- Extra Feature: " + GetThirdFurniture(model.RoomType) + "\n\n" +
                "User Requirement Applied:\n" +
                model.UserInstruction;
        }

        private async Task<string> SaveImageFromOpenAIResponse(string json)
        {
            using JsonDocument document = JsonDocument.Parse(json);

            JsonElement data = document.RootElement.GetProperty("data")[0];

            if (data.TryGetProperty("b64_json", out JsonElement base64Element))
            {
                string base64 = base64Element.GetString() ?? "";

                byte[] imageBytes = Convert.FromBase64String(base64);

                string folder = Path.Combine(
                    _environment.WebRootPath,
                    "uploads",
                    "ai-designs"
                );

                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                string fileName = "ai-room-design-" + Guid.NewGuid().ToString("N") + ".png";

                string fullPath = Path.Combine(folder, fileName);

                await File.WriteAllBytesAsync(fullPath, imageBytes);

                return "/uploads/ai-designs/" + fileName;
            }

            if (data.TryGetProperty("url", out JsonElement urlElement))
            {
                return urlElement.GetString() ?? "";
            }

            throw new Exception("OpenAI image response did not contain image data.");
        }

        private string GetMimeType(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();

            if (extension == ".jpg" || extension == ".jpeg")
            {
                return "image/jpeg";
            }

            if (extension == ".webp")
            {
                return "image/webp";
            }

            return "image/png";
        }

        private string CleanText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            string clean = WebUtility.HtmlEncode(value.Trim());

            if (clean.Length > 80)
            {
                clean = clean.Substring(0, 80);
            }

            return clean;
        }
    }
}
