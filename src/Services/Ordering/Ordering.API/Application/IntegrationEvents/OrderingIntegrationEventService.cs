﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.eShopOnContainers.BuildingBlocks.EventBus.Abstractions;
using Microsoft.eShopOnContainers.BuildingBlocks.EventBus.Events;
using Microsoft.eShopOnContainers.BuildingBlocks.IntegrationEventLogEF;
using Microsoft.eShopOnContainers.BuildingBlocks.IntegrationEventLogEF.Services;
using Microsoft.eShopOnContainers.BuildingBlocks.IntegrationEventLogEF.Utilities;
using Microsoft.eShopOnContainers.Services.Ordering.Infrastructure;
using System;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Ordering.API.Application.IntegrationEvents
{
    public class OrderingIntegrationEventService : IOrderingIntegrationEventService
    {
        private readonly Func<DbConnection, IIntegrationEventLogService> _integrationEventLogServiceFactory;
        private readonly IEventBus _eventBus;
        private readonly OrderingContext _orderingContext;
        private readonly IntegrationEventLogContext _eventLogContext;
        private readonly IIntegrationEventLogService _eventLogService;

        public OrderingIntegrationEventService(IEventBus eventBus, 
            OrderingContext orderingContext,
            IntegrationEventLogContext eventLogContext,
            Func<DbConnection, IIntegrationEventLogService> integrationEventLogServiceFactory)
        {
            _orderingContext = orderingContext ?? throw new ArgumentNullException(nameof(orderingContext));
            _eventLogContext = eventLogContext ?? throw new ArgumentNullException(nameof(eventLogContext));
            _integrationEventLogServiceFactory = integrationEventLogServiceFactory ?? throw new ArgumentNullException(nameof(integrationEventLogServiceFactory));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _eventLogService = _integrationEventLogServiceFactory(_orderingContext.Database.GetDbConnection());
        }

        public async Task PublishThroughEventBusAsync(IntegrationEvent evt)
        {
            await SaveEventAsync(evt);
            _eventBus.Publish(evt);
            await _eventLogService.MarkEventAsPublishedAsync(evt);
        }

        private async Task SaveEventAsync(IntegrationEvent evt)
        {
            var strategy = _orderingContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await _orderingContext.BeginTransactionAsync();
                await _eventLogService.SaveEventAsync(evt, _orderingContext.GetCurrentTransaction.GetDbTransaction());
                await _orderingContext.CommitTransactionAsync();
            });
            
        }
    }
}
