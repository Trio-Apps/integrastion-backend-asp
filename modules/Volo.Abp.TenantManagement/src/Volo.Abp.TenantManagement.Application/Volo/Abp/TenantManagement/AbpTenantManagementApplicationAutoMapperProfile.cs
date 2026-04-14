using AutoMapper;
using Foodics;
using Volo.Abp.TenantManagement.Smtp;
using Volo.Abp.TenantManagement.Talabat;

namespace Volo.Abp.TenantManagement;

public class AbpTenantManagementApplicationAutoMapperProfile : Profile
{
    public AbpTenantManagementApplicationAutoMapperProfile()
    {
        CreateMap<Tenant, TenantDto>()
            .MapExtraProperties();

        CreateMap<FoodicsAccountDto, FoodicsAccount>()
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.TalabatOrderTagId, opt => opt.Ignore())
            .ReverseMap();
        
        CreateMap<TalabatAccountDto, TalabatAccount>()
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.FoodicsAccount, opt => opt.Ignore())
            .ForMember(dest => dest.Password, opt => opt.Ignore())
            .ForMember(dest => dest.DefaultFoodicsCustomerId, opt => opt.Ignore())
            .ForMember(dest => dest.DefaultFoodicsCustomerName, opt => opt.Ignore())
            .ForMember(dest => dest.DefaultFoodicsCustomerAddressId, opt => opt.Ignore())
            .ForMember(dest => dest.DefaultFoodicsCustomerAddressName, opt => opt.Ignore())
            .ReverseMap();

        CreateMap<SmtpConfigDto, SmtpConfig>()
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ReverseMap();
    }
}
