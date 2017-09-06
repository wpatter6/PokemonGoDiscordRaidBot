using System;
using System.Collections.Generic;
using System.Text;
using AutoMapper;
using PokemonGoRaidBot.Objects;
using PokemonGoRaidBot.Data.Objects;

namespace PokemonGoRaidBot.Data
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            CreateMap<PokemonRaidPost, RaidPostEntity>();
                //.ForMember(dest => dest.)//TODO!!
        }
    }
}
