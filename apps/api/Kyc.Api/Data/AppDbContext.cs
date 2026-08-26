using Microsoft.EntityFrameworkCore;

namespace Kyc.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options);
