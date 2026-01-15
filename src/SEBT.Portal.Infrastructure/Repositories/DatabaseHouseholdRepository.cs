using Microsoft.EntityFrameworkCore;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;

namespace SEBT.Portal.Infrastructure.Repositories;

/// <summary>
/// Database-backed implementation of <see cref="IHouseholdRepository"/> using Entity Framework Core.
/// </summary>
/// <param name="dbContext">The database context for accessing household data.</param>
public class DatabaseHouseholdRepository(PortalDbContext dbContext) : IHouseholdRepository
{
    public async Task<HouseholdData?> GetHouseholdByEmailAsync(
        string email,
        bool includeAddress = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalizedEmail = NormalizeEmail(email);

        var query = dbContext.Households
            .AsNoTracking()
            .Include(h => h.Children)
            .AsQueryable();

        if (includeAddress)
        {
            query = query.Include(h => h.Address);
        }

        var entity = await query
            .FirstOrDefaultAsync(h => h.Email == normalizedEmail, cancellationToken);

        return entity == null ? null : MapToDomainModel(entity, includeAddress);
    }

    public async Task UpsertHouseholdAsync(
        HouseholdData householdData,
        CancellationToken cancellationToken = default)
    {
        if (householdData == null)
        {
            throw new ArgumentNullException(nameof(householdData));
        }

        if (string.IsNullOrWhiteSpace(householdData.Email))
        {
            throw new ArgumentException("Email cannot be null or empty.", nameof(householdData));
        }

        var normalizedEmail = NormalizeEmail(householdData.Email);
        var existingEntity = await dbContext.Households
            .Include(h => h.Children)
            .Include(h => h.Address)
            .FirstOrDefaultAsync(h => h.Email == normalizedEmail, cancellationToken);

        if (existingEntity == null)
        {
            // Create new household
            var newEntity = MapToEntity(householdData, normalizedEmail);
            dbContext.Households.Add(newEntity);
        }
        else
        {
            // Update existing household
            UpdateEntity(existingEntity, householdData, normalizedEmail);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Normalizes an email address to lowercase for consistent storage and comparison.
    /// </summary>
    /// <param name="email">The email address to normalize.</param>
    /// <returns>The normalized (lowercase) email address.</returns>
    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static HouseholdData MapToDomainModel(HouseholdEntity entity, bool includeAddress)
    {
        var householdData = new HouseholdData
        {
            Email = entity.Email,
            Phone = entity.Phone,
            BenefitIssueDate = entity.BenefitIssueDate,
            BenefitExpirationDate = entity.BenefitExpirationDate,
            Last4DigitsOfCard = entity.Last4DigitsOfCard,
            ApplicationNumber = entity.ApplicationNumber,
            CaseNumber = entity.CaseNumber,
            ApplicationStatus = (ApplicationStatus)entity.ApplicationStatus,
            Children = entity.Children.Select(c => new Child
            {
                FirstName = c.FirstName,
                LastName = c.LastName
            }).ToList()
        };

        // Only include address if explicitly requested (ID verification check)
        if (includeAddress && entity.Address != null)
        {
            householdData.AddressOnFile = new Address
            {
                StreetAddress1 = entity.Address.StreetAddress1,
                StreetAddress2 = entity.Address.StreetAddress2,
                City = entity.Address.City,
                State = entity.Address.State,
                PostalCode = entity.Address.PostalCode
            };
        }

        return householdData;
    }

    private static HouseholdEntity MapToEntity(HouseholdData householdData, string normalizedEmail)
    {
        var entity = new HouseholdEntity
        {
            Email = normalizedEmail,
            Phone = householdData.Phone,
            BenefitIssueDate = householdData.BenefitIssueDate,
            BenefitExpirationDate = householdData.BenefitExpirationDate,
            Last4DigitsOfCard = householdData.Last4DigitsOfCard,
            ApplicationNumber = householdData.ApplicationNumber,
            CaseNumber = householdData.CaseNumber,
            ApplicationStatus = (int)householdData.ApplicationStatus,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Children = householdData.Children.Select(c => new ChildEntity
            {
                FirstName = c.FirstName,
                LastName = c.LastName
            }).ToList()
        };

        // Add address if provided
        if (householdData.AddressOnFile != null)
        {
            entity.Address = new AddressEntity
            {
                StreetAddress1 = householdData.AddressOnFile.StreetAddress1,
                StreetAddress2 = householdData.AddressOnFile.StreetAddress2,
                City = householdData.AddressOnFile.City,
                State = householdData.AddressOnFile.State,
                PostalCode = householdData.AddressOnFile.PostalCode
            };
        }

        return entity;
    }

    private void UpdateEntity(
        HouseholdEntity existingEntity,
        HouseholdData householdData,
        string normalizedEmail)
    {
        // Update main household properties
        existingEntity.Phone = householdData.Phone;
        existingEntity.BenefitIssueDate = householdData.BenefitIssueDate;
        existingEntity.BenefitExpirationDate = householdData.BenefitExpirationDate;
        existingEntity.Last4DigitsOfCard = householdData.Last4DigitsOfCard;
        existingEntity.ApplicationNumber = householdData.ApplicationNumber;
        existingEntity.CaseNumber = householdData.CaseNumber;
        existingEntity.ApplicationStatus = (int)householdData.ApplicationStatus;
        existingEntity.UpdatedAt = DateTime.UtcNow;

        // Update children - remove existing and add new ones
        dbContext.Children.RemoveRange(existingEntity.Children);
        existingEntity.Children = householdData.Children.Select(c => new ChildEntity
        {
            HouseholdId = existingEntity.Id,
            FirstName = c.FirstName,
            LastName = c.LastName
        }).ToList();

        // Update address
        if (householdData.AddressOnFile != null)
        {
            if (existingEntity.Address == null)
            {
                existingEntity.Address = new AddressEntity
                {
                    HouseholdId = existingEntity.Id
                };
            }

            existingEntity.Address.StreetAddress1 = householdData.AddressOnFile.StreetAddress1;
            existingEntity.Address.StreetAddress2 = householdData.AddressOnFile.StreetAddress2;
            existingEntity.Address.City = householdData.AddressOnFile.City;
            existingEntity.Address.State = householdData.AddressOnFile.State;
            existingEntity.Address.PostalCode = householdData.AddressOnFile.PostalCode;
        }
        else if (existingEntity.Address != null)
        {
            // Remove address if it's no longer provided
            dbContext.Addresses.Remove(existingEntity.Address);
            existingEntity.Address = null;
        }
    }
}
