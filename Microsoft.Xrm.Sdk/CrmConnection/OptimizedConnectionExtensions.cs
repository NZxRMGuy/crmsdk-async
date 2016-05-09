using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrmConnection
{
    public static class OptimizedConnectionExtensions
    {
        public async static Task<T> ExecuteAsync<T>(this IOrganizationService sdk, OrganizationRequest request) where T : OrganizationResponse
        {
            var t = Task.Factory.StartNew(() =>
            {
                var response = sdk.Execute(request) as T;
                return response;
            });

            return await t;
        }

        public async static Task<Entity> RetrieveAsync(this IOrganizationService sdk, string entityName, Guid id, ColumnSet columnSet)
        {
            var t = Task.Factory.StartNew(() =>
            {
                var response = sdk.Retrieve(entityName, id, columnSet);
                return response;
            });

            return await t;
        }

        public async static Task<EntityCollection> RetrieveMultipleAsync(this IOrganizationService sdk, QueryBase query)
        {
            var t = Task.Factory.StartNew(() =>
            {
                var response = sdk.RetrieveMultiple(query);
                return response;
            });

            return await t;
        }

        public async static Task<Guid> CreateAsync(this IOrganizationService sdk, Entity entity)
        {
            var t = Task.Factory.StartNew(() =>
            {
                var response = sdk.Create(entity);
                return response;
            });

            return await t;
        }

        public async static Task AssociateAsync(this IOrganizationService sdk, string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            var t = Task.Factory.StartNew(() =>
            {
                sdk.Associate(entityName, entityId, relationship, relatedEntities);
            });

            await t;
        }

        public async static Task DeleteAsync(this IOrganizationService sdk, string entityName, Guid id)
        {
            var t = Task.Factory.StartNew(() =>
            {
                sdk.Delete(entityName, id);
            });

            await t;
        }

        public async static Task DisassociateAsync(this IOrganizationService sdk, string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            var t = Task.Factory.StartNew(() =>
            {
                sdk.Disassociate(entityName, entityId, relationship, relatedEntities);
            });

            await t;
        }

        public async static Task UpdateAsync(this IOrganizationService sdk, Entity entity)
        {
            var t = Task.Factory.StartNew(() =>
            {
                sdk.Update(entity);
            });

            await t;
        }
    }
}
