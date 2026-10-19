using AutoMapper;
using ConnectLive.Portal.Shared.Request;
using ConnectLive.Portal.Shared.Response;
using ConntectLive.DAL;

namespace ConnectLive.Core.Api.Configurations;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<SaveUserRequest, UserEntity>()
            .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.UserFirstName));

        // La signature (jeton d'accès) n'est jamais déduite des données : elle est émise une seule fois,
        // à l'enregistrement, par le handler (voir docs/AUTHENTIFICATION.md). Les profils n'exposent aucun secret.
        CreateMap<UserEntity, SaveUserResponse>()
            .ForMember(dest => dest.Signature, opt => opt.Ignore());
    }
}
