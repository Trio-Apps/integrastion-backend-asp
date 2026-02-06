using AutoMapper;
using OrderXChange.Application.Versioning.DTOs;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using OrderXChange.Domain.Versioning;
using OrderXChange.Domain.Staging;
using System;

namespace OrderXChange;

public class OrderXChangeApplicationAutoMapperProfile : Profile
{
    public OrderXChangeApplicationAutoMapperProfile()
    {
        /* You can configure your AutoMapper mapping configuration here.
         * Alternatively, you can split your mapping configurations
         * into multiple profile classes for a better organization. */

        // Menu Group mappings
        CreateMap<FoodicsMenuGroup, MenuGroupDto>()
            .ForMember(dest => dest.ActiveCategoriesCount, opt => opt.Ignore())
            .ForMember(dest => dest.TotalProductsCount, opt => opt.Ignore())
            .ForMember(dest => dest.LastSyncedAt, opt => opt.Ignore())
            .ForMember(dest => dest.LastSyncStatus, opt => opt.Ignore())
            .ForMember(dest => dest.Categories, opt => opt.Ignore());

        CreateMap<CreateMenuGroupDto, FoodicsMenuGroup>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => true))
            .ForMember(dest => dest.Categories, opt => opt.Ignore());

        CreateMap<MenuGroupCategory, MenuGroupCategoryDto>()
            .ForMember(dest => dest.CategoryName, opt => opt.Ignore())
            .ForMember(dest => dest.ProductsCount, opt => opt.Ignore());

        CreateMap<AssignCategoryDto, MenuGroupCategory>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.MenuGroupId, opt => opt.Ignore())
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => true))
            .ForMember(dest => dest.AssignedAt, opt => opt.MapFrom(src => DateTime.UtcNow));

        // Menu Group Talabat Mapping mappings
        CreateMap<MenuGroupTalabatMapping, MenuGroupTalabatMappingDto>()
            .ForMember(dest => dest.MenuGroupName, opt => opt.MapFrom(src => src.MenuGroup != null ? src.MenuGroup.Name : string.Empty))
            .ForMember(dest => dest.Configuration, opt => opt.MapFrom(src => 
                !string.IsNullOrWhiteSpace(src.ConfigurationJson)
                    ? System.Text.Json.JsonSerializer.Deserialize<MenuGroupMappingConfigurationDto>(
                        src.ConfigurationJson,
                        (System.Text.Json.JsonSerializerOptions?)null)
                    : null));

        CreateMap<CreateMenuGroupTalabatMappingDto, MenuGroupTalabatMapping>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.FoodicsAccountId, opt => opt.Ignore())
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => true))
            .ForMember(dest => dest.MappingEstablishedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.LastVerifiedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.SyncCount, opt => opt.MapFrom(src => 0))
            .ForMember(dest => dest.IsTalabatValidated, opt => opt.MapFrom(src => false))
            .ForMember(dest => dest.SyncStatus, opt => opt.MapFrom(src => MenuMappingSyncStatus.Pending))
            .ForMember(dest => dest.ConfigurationJson, opt => opt.MapFrom(src => 
                src.Configuration != null
                    ? System.Text.Json.JsonSerializer.Serialize(
                        src.Configuration,
                        (System.Text.Json.JsonSerializerOptions?)null)
                    : null));

        CreateMap<UpdateMenuGroupTalabatMappingDto, MenuGroupTalabatMapping>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.FoodicsAccountId, opt => opt.Ignore())
            .ForMember(dest => dest.MenuGroupId, opt => opt.Ignore())
            .ForMember(dest => dest.TalabatVendorCode, opt => opt.Ignore())
            .ForMember(dest => dest.TalabatMenuId, opt => opt.Ignore())
            .ForMember(dest => dest.MappingEstablishedAt, opt => opt.Ignore())
            .ForMember(dest => dest.SyncCount, opt => opt.Ignore())
            .ForMember(dest => dest.IsTalabatValidated, opt => opt.Ignore())
            .ForMember(dest => dest.TalabatInternalMenuId, opt => opt.Ignore())
            .ForMember(dest => dest.LastSyncError, opt => opt.Ignore())
            .ForMember(dest => dest.LastVerifiedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.ConfigurationJson, opt => opt.MapFrom(src => 
                src.Configuration != null
                    ? System.Text.Json.JsonSerializer.Serialize(
                        src.Configuration,
                        (System.Text.Json.JsonSerializerOptions?)null)
                    : null));

        // Talabat order sync log mappings
        CreateMap<TalabatOrderSyncLog, TalabatOrderLogDto>();
    }
}
