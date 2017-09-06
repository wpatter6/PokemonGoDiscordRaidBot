using System;
using PokemonGoRaidBot.Config;
using AutoMapper;
using PokemonGoRaidBot.Interfaces;

namespace PokemonGoRaidBot.Services
{
    public class StatMapper : IStatMapper
    {
        private IMapper _mapper;
        public StatMapper()
        {
            _mapper = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<MapperProfile>();
            }).CreateMapper();
        }

        public void Dispose()
        {
            _mapper = null;
        }

        public TDestination Map<TDestination>(object source)
        {
            return _mapper.Map<TDestination>(source);
        }

        public TDestination Map<TSource, TDestination>(TSource source)
        {
            return _mapper.Map<TSource, TDestination>(source);
        }

        public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
        {
            return _mapper.Map(source, destination);
        }
    }
}
