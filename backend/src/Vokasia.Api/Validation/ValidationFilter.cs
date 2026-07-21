using System.Collections;
using FluentValidation;
using FluentValidation.Results;

namespace Vokasia.Api.Validation;

/// <summary>
/// VOK-H3-E3 §2: endpoint filter GLOBAL — dipasang sekali per <c>MapGroup</c> (lihat masing-masing
/// <c>Endpoints/*.cs</c>: <c>.AddEndpointFilter&lt;ValidationFilter&gt;()</c>), BUKAN per-route
/// per-request-type. Untuk setiap argumen handler minimal-API, filter ini mencoba resolve
/// <c>IValidator&lt;T&gt;</c> dari DI (FluentValidation, auto-scan lewat
/// <c>AddValidatorsFromAssemblyContaining&lt;Program&gt;()</c> di Program.cs) sesuai TIPE RUNTIME
/// argumen tsb; kalau ketemu, jalankan; kalau tidak (request type belum punya validator terdaftar),
/// argumen itu DILEWATKAN APA ADANYA oleh filter ini — filter ini generik, TIDAK menolak request
/// hanya krn tak ada validator (itu bukan tanggung jawabnya).
///
/// Cakupan "semua request type H1–H3 WAJIB punya validator terdaftar" ditegakkan test TERPISAH
/// (<c>ValidatorCoverageTests</c>, reflection thd daftar tetap 8 request type yang disebut eksplisit
/// ticket H3-E3 §2) — BUKAN oleh filter runtime ini, supaya filter tetap generik tanpa whitelist
/// type hardcoded di sini.
///
/// Mendukung <c>List&lt;T&gt;</c>/<c>IEnumerable&lt;T&gt;</c> (mis. <c>BulkCreatePlacements(List&lt;CreatePlacementRequest&gt;)</c>)
/// — tiap elemen divalidasi, error digabung dgn prefix indeks <c>"[i].Field"</c> supaya FE tahu baris
/// mana yang gagal.
/// </summary>
public class ValidationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var ct = context.HttpContext.RequestAborted;
        var failures = new List<ValidationFailure>();

        foreach (var arg in context.Arguments)
        {
            if (arg is null)
            {
                continue;
            }

            var argType = arg.GetType();
            var itemType = GetEnumerableItemType(argType);

            if (itemType is not null && arg is IEnumerable enumerable)
            {
                var itemValidator = ResolveValidator(services, itemType);
                if (itemValidator is null)
                {
                    continue;
                }

                var index = 0;
                foreach (var item in enumerable)
                {
                    if (item is not null)
                    {
                        var result = await itemValidator.ValidateAsync(new ValidationContext<object>(item), ct);
                        foreach (var f in result.Errors)
                        {
                            failures.Add(new ValidationFailure($"[{index}].{f.PropertyName}", f.ErrorMessage));
                        }
                    }
                    index++;
                }

                continue;
            }

            var validator = ResolveValidator(services, argType);
            if (validator is null)
            {
                continue;
            }

            var directResult = await validator.ValidateAsync(new ValidationContext<object>(arg), ct);
            failures.AddRange(directResult.Errors);
        }

        if (failures.Count > 0)
        {
            var grouped = failures
                .GroupBy(f => f.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray());
            return Results.ValidationProblem(grouped);
        }

        return await next(context);
    }

    private static IValidator? ResolveValidator(IServiceProvider services, Type type)
    {
        var validatorType = typeof(IValidator<>).MakeGenericType(type);
        return services.GetService(validatorType) as IValidator;
    }

    /// <summary>Null utk string (IEnumerable&lt;char&gt; scr teknis, tapi bukan maksud "koleksi item request" di sini) dan tipe non-koleksi.</summary>
    private static Type? GetEnumerableItemType(Type type)
    {
        if (type == typeof(string))
        {
            return null;
        }

        var enumerableInterface = type.GetInterfaces().Prepend(type)
            .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return enumerableInterface?.GetGenericArguments()[0];
    }
}
