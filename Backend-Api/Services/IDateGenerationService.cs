using Backend_Api.Dtos;

namespace Backend_Api.Services;

public interface IDateGenerationService
{
    FakeDateGeneratedDto Generate();
}
