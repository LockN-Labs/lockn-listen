using LockNListen.Domain.Models;
using LockNListen.Domain.Services;
using Microsoft.AspNetCore.Mvc;

namespace LockNListen.Api.Endpoints
{
    /// <summary>
    /// Request body for audio classification.
    /// </summary>
    public class ClassifyRequest
    {
        /// <summary>
        /// Raw audio data as base64-encoded bytes.
        /// </summary>
        public required byte[] AudioData { get; set; }

        /// <summary>
        /// Sample rate of the audio (e.g., 16000).
        /// </summary>
        public int SampleRate { get; set; } = 16000;
    }

    public static class ClassificationEndpoints
    {
        public static void MapClassifyEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api").WithTags("Classification");

            group.MapPost("/classify", Classify)
                .Produces<SoundClassification>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest);
        }

        private static async Task<SoundClassification> Classify([FromBody] ClassifyRequest request, ISoundClassifier classifier)
        {
            return await classifier.ClassifyAsync(request.AudioData, request.SampleRate);
        }
    }
}
