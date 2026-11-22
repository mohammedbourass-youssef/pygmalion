using Backend_Api.Dtos;
using Backend_Api.Repositories;

namespace Backend_Api.Services;

public sealed class DateGenerationService(IDateRepository dateRepository) : IDateGenerationService
{
    public FakeDateGeneratedDto Generate() => new(dateRepository.GenerateDate());
}
