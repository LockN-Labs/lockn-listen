using LockNListen.Domain.Models;
using LockNListen.Domain.Services;
using Microsoft.AspNetCore.Mvc;

namespace LockNListen.Api.Endpoints
{
    public static class ClassificationEndpoints
    {
        public static void MapClassifyEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api").WithTags("Classification");

            group.MapPost("/classify", Classify)
                .Produces<SoundClassification>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest);
        }

        private static async Task<SoundClassification> Classify([FromBody] byte[] audioData, [FromBody] int sampleRate, ISoundClassifier classifier)
        {
            return await classifier.ClassifyAsync(audioData, sampleRate);
        }
    }
}
