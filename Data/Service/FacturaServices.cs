using FactuSystem.Data.Context;
using FactuSystem.Data.Model;
using FactuSystem.Data.Request;
using FactuSystem.Data.Response;
using Microsoft.EntityFrameworkCore;

namespace FactuSystem.Data.Services;

public class FacturaServices : IFacturaServices
{
    private readonly IMyDbContext dbContext;

    public FacturaServices(IMyDbContext dbContext)
    {
        this.dbContext = dbContext;
    }
    public async Task<decimal> CalcularTotalDineroEnCaja()
    {
        var resultado = await Consultar();
        var result = await PaymentQuery();
        if (resultado.Success && result.Success)
        {
            var totalFacturas = resultado.Data.Sum(factura => factura.SaldoPagado);

            var totalPagos = result.Data.Sum(pago => (decimal)pago.MontoPagado);

            // Retorna la suma total de dinero en caja (suma de facturas y pagos)
            return totalFacturas + totalPagos;

        }


        return 0; // O cualquier valor predeterminado en caso de error
    }

    public async Task<Result<List<PagoResponse>>> PaymentQuery()
    {
        try
        {
            var pagos = await dbContext.Pagos
                .Select(p => p.ToResponse())
                .ToListAsync();

            return new Result<List<PagoResponse>>()
            {
                Message = "Ok",
                Success = true,
                Data = pagos
            };
        }
        catch (Exception ex)
        {
            return new Result<List<PagoResponse>>()
            {
                Message = ex.Message,
                Success = false
            };
        }
    }
   
    public async Task<Result<List<FacturaResponse>>> Consultar()
    {
        try
        {
            var facturas = await dbContext.Facturas
                .AsNoTracking()
                .IgnoreAutoIncludes()
                .Include(f => f.Cliente)
                .Include(f => f.Pagos)
                .Include(f => f.Detalles)
                .ThenInclude(d => d.Producto)
                .Select(f => f.ToResponse())
                .ToListAsync();
            return new Result<List<FacturaResponse>>()
            {
                Data = facturas,
                Success = true,
                Message = "Ok"
            };
        }
        catch (Exception E)
        {
            return new Result<List<FacturaResponse>>()
            {
                Data = null,
                Success = false,
                Message = E.Message
            };
        }
    }

    public async Task<Result<FacturaResponse>> Crear(FacturaRequest request)
    {
        try
        {
            var factura = Factura.Crear(request);
            dbContext.Facturas.Add(factura);
            await dbContext.SaveChangesAsync();
            return new Result<FacturaResponse>()
            {
                Data = factura.ToResponse(),
                Success = true,
                Message = "Ok ✔"
            };
        }
        catch (Exception E)
        {
            return new Result<FacturaResponse>()
            {
                Data = null,
                Success = false,
                Message = E.Message
            };
        }
    }
    public async Task<Result> Eliminar(FacturaRequest request)
    {
        try
        {
            var contacto = await dbContext.Facturas
                .FirstOrDefaultAsync(c => c.Id == request.Id);
            if (contacto == null)
                return new Result() { Message = "No se encontro el usuario", Success = false };

            dbContext.Facturas.Remove(contacto);
            await dbContext.SaveChangesAsync();
            return new Result() { Message = "Ok", Success = true };
        }
        catch (Exception E)
        {

            return new Result() { Message = E.Message, Success = false };
        }
    }
    public async Task<Result<List<FacturaResponse>>> BuscarFacturas(DateTime? fecha)
    {
        try
        {
            var query = dbContext.Facturas
                .Include(f => f.Detalles)
                .ThenInclude(d => d.Producto)
                .AsQueryable();

            if (fecha.HasValue)
            {
                query = query.Where(f => EF.Functions.DateDiffDay(f.Fecha, fecha.Value) == 0);
            }

            var facturas = await query.Select(f => f.ToResponse()).ToListAsync();

            return new Result<List<FacturaResponse>>()
            {
                Data = facturas,
                Success = true,
                Message = "Búsqueda exitosa"
            };
        }
        catch (Exception ex)
        {
            return new Result<List<FacturaResponse>>()
            {
                Data = null,
                Success = false,
                Message = ex.Message
            };
        }
    }

    public async Task<Result<FacturaResponse>> Modificar(FacturaRequest request)
    {
        try
        {
            var factura = await dbContext.Facturas
                .Include(f => f.Detalles)
                .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(c => c.Id == request.Id);

            if (factura == null)
                return new Result<FacturaResponse>() { Message = "No se encontró la factura", Success = false };

            // Actualizar las propiedades de la factura según el request
            factura.ClienteId = request.ClienteId;
            factura.TypePayment = request.TypePayment;
            factura.SaldoPagado = request.SaldoPagado;
            factura.SaldoPendiente = request.SaldoPendiente;
            factura.Detalles = request.Detalles
                .Select(detalle => FacturaDetalle.Crear(detalle))
                .ToList();

            // Guardar los cambios en la base de datos
            await dbContext.SaveChangesAsync();

            return new Result<FacturaResponse>()
            {
                Data = factura.ToResponse(),
                Success = true,
                Message = "Factura modificada correctamente"
            };
        }
        catch (Exception ex)
        {
            return new Result<FacturaResponse>()
            {
                Data = null,
                Success = false,
                Message = ex.Message
            };
        }
    }
    public async Task<Result<List<FacturaDetalleResponse>>> ObtenerDetallesUltimaFactura()
    {
        try
        {
            // Obtener la última factura facturada
            var ultimaFactura = await dbContext.Facturas
                .Include(f => f.Detalles)
                .OrderByDescending(f => f.Fecha)
                .FirstOrDefaultAsync();

            if (ultimaFactura == null)
            {
                return new Result<List<FacturaDetalleResponse>>()
                {
                    Success = false,
                    Message = "No se encontró ninguna factura facturada"
                };
            }

            // Obtener los detalles de la última factura
            var detalles = ultimaFactura.Detalles
                .Select(d => d.ToResponse())
                .ToList();

            return new Result<List<FacturaDetalleResponse>>()
            {
                Success = true,
                Message = "Detalles de la última factura obtenidos correctamente",
                Data = detalles
            };
        }
        catch (Exception ex)
        {
            return new Result<List<FacturaDetalleResponse>>()
            {
                Success = false,
                Message = ex.Message
            };
        }
    }
    public async Task<Result<FacturaResponse>> ObtenerDetallesUltimaFacturaConTipoVenta()
    {
        try
        {
            // Obtener todas las facturas
            var todasLasFacturas = await dbContext.Facturas
                .Include(f => f.Detalles)
                .ToListAsync();

            // Ordenar las facturas por fecha de manera descendente
            var facturasOrdenadas = todasLasFacturas.OrderByDescending(f => f.Fecha);

            // Encontrar la última factura que no esté completamente pagada
            var ultimaFactura = facturasOrdenadas.FirstOrDefault(f => f.SaldoPendiente > 0);

            if (ultimaFactura == null)
            {
                return new Result<FacturaResponse>()
                {
                    Success = false,
                    Message = "No se encontró ninguna factura pendiente de pago"
                };
            }

            // Crear un objeto FacturaResponse con los detalles y el tipo de venta
            var facturaResponse = new FacturaResponse
            {
                Detalles = ultimaFactura.Detalles.Select(d => d.ToResponse()).ToList(),
                TypePayment = ultimaFactura.TypePayment,
                SaldoPagado = ultimaFactura.SaldoPagado
            };

            return new Result<FacturaResponse>()
            {
                Success = true,
                Message = "Detalles de la última factura obtenidos correctamente",
                Data = facturaResponse
            };
        }
        catch (Exception ex)
        {
            return new Result<FacturaResponse>()
            {
                Success = false,
                Message = ex.Message
            };
        }
    }
    public async Task<Result<FacturaResponse>> ObtenerUltimaFacturaContado()
    {
        try
        {
            var factura = await dbContext.Facturas
                .Include(f => f.Detalles)
                .OrderByDescending(f => f.Fecha)
                .FirstOrDefaultAsync(f => f.TypePayment == "1"); // Filtrar por tipo de venta al contado

            if (factura == null)
            {
                return new Result<FacturaResponse>()
                {
                    Success = false,
                    Message = "No se encontró ninguna factura de venta al contado"
                };
            }

            return new Result<FacturaResponse>()
            {
                Success = true,
                Message = "Última factura de venta al contado obtenida correctamente",
                Data = factura.ToResponse()
            };
        }
        catch (Exception ex)
        {
            return new Result<FacturaResponse>()
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public async Task<Result<FacturaResponse>> ObtenerUltimaFacturaCredito()
    {
        try
        {
            var factura = await dbContext.Facturas
                .Include(f => f.Detalles)
                .OrderByDescending(f => f.Fecha)
                .FirstOrDefaultAsync(f => f.TypePayment == "2"); // Filtrar por tipo de venta a crédito

            if (factura == null)
            {
                return new Result<FacturaResponse>()
                {
                    Success = false,
                    Message = "No se encontró ninguna factura de venta a crédito"
                };
            }

            return new Result<FacturaResponse>()
            {
                Success = true,
                Message = "Última factura de venta a crédito obtenida correctamente",
                Data = factura.ToResponse()
            };
        }
        catch (Exception ex)
        {
            return new Result<FacturaResponse>()
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

}


public interface IFacturaServices
{
    Task<decimal> CalcularTotalDineroEnCaja();

    Task<Result<List<FacturaResponse>>> Consultar();
    Task<Result<FacturaResponse>> Crear(FacturaRequest request);
    Task<Result> Eliminar(FacturaRequest request);
    Task<Result<List<FacturaResponse>>> BuscarFacturas(DateTime? fecha);
    Task<Result<FacturaResponse>> Modificar(FacturaRequest request);
    Task<Result<List<FacturaDetalleResponse>>> ObtenerDetallesUltimaFactura();
    Task<Result<FacturaResponse>> ObtenerDetallesUltimaFacturaConTipoVenta();
    Task<Result<FacturaResponse>> ObtenerUltimaFacturaCredito();
    Task<Result<FacturaResponse>> ObtenerUltimaFacturaContado();


    }