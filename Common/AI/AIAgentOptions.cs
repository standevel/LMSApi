namespace LMS.Api.Common.AI;

public class AIAgentOptions
{
    public const string SectionName = "AIAgentSettings";

    public bool Enabled { get; set; } = true;
    public string ApiKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = "https://api.openai.com/v1";
    public string ModelId { get; set; } = "gpt-4o";
    public string EmbeddingModelId { get; set; } = "text-embedding-3-small";
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 2000;
}
