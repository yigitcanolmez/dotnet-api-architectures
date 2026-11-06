namespace VerticalSlice.Api.Common;

// [Common] REPR'deki "E" (Endpoint). Her dilim kendi endpoint'ini bu arayüzle tanımlar;
// Program.cs hiçbir özelliği tek tek bilmez (bkz. EndpointExtensions).
public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}

// Dilim handler'larını işaretlemek için marker arayüz. MediatR yok:
// handler'lar DI'a kaydedilir ve endpoint lambda'sına doğrudan enjekte edilir.
public interface IHandler;
