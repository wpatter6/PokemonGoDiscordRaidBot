using System;
using System.Collections.Generic;
using System.Text;
using AutoMapper;
using PokemonGoRaidBot.Objects;
using PokemonGoRaidBot.Data.Entities;
using Discord.WebSocket;

namespace PokemonGoRaidBot.Configuration
{
    public class MapperProfile : Profile
    {
        public MapperProfile()
        {
            CreateMap<SocketGuild, DiscordServerEntity>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
                .ForMember(dest => dest.Channels, opt => opt.MapFrom(src => src.Channels))
                .MaxDepth(1);

            CreateMap<SocketGuildChannel, DiscordChannelEntity>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
                .ForMember(dest => dest.ServerId, opt => opt.MapFrom(src => src.Guild.Id))
                .ForMember(dest => dest.Server, opt => opt.MapFrom(src => src.Guild))
                .MaxDepth(1);

            CreateMap<PokemonRaidPost, PokemonEntity>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PokemonId))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.PokemonName));

            //CreateMap<SocketUser, DiscordUserEntity>()
            //    .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Username));

            CreateMap<PokemonRaidPost, RaidPostLocationEntity>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Location))
                .ForMember(dest => dest.ServerId, opt => opt.MapFrom(src => src.GuildId))
                .ForMember(dest => dest.Latitude, opt => opt.MapFrom(src => src.LatLong == null ? null : src.LatLong.Latitude))
                .ForMember(dest => dest.Longitude, opt => opt.MapFrom(src => src.LatLong == null ? null : src.LatLong.Longitude));

            CreateMap<PokemonRaidPost, RaidPostEntity>()
                .ForMember(dest => dest.Pokemon, opt => opt.MapFrom(src => new PokemonInfo() { Id = src.PokemonId, Name = src.PokemonName }))
                .ForMember(dest => dest.PostedDate, opt => opt.MapFrom(src => src.PostDate))
                .ForMember(dest => dest.ResponseCount, opt => opt.MapFrom(src => src.Responses.Count))
                .ForMember(dest => dest.JoinCount, opt => opt.MapFrom(src => src.JoinedUsers.Count))
                .ForMember(dest => dest.ServerId, opt => opt.MapFrom(src => src.GuildId))
                .ForMember(dest => dest.CreationMessage, opt => opt.MapFrom(src => src.Responses.Count > 0 ? src.Responses[0].Content : null))
                .ForMember(dest => dest.Location, opt => opt.Ignore());
        }
    }
}
