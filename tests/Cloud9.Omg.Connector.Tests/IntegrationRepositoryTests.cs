using System;
using System.IO;
using Cloud9.Omg.Connector.Models;
using Cloud9.Omg.Connector.Persistence;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Cloud9.Omg.Connector.Tests
{
    public sealed class IntegrationRepositoryTests : IDisposable
    {
        private readonly string _databasePath = Path.Combine(
            Path.GetTempPath(),
            "omg-connector-test-" + Guid.NewGuid().ToString("N") + ".db");

        [Fact]
        public void PersistsCursorAndOrderAcrossRepositoryRestart()
        {
            var cursor = new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero);
            var order = CreateOrder("2026-08-30T10:00:00Z");

            using (var first = new IntegrationRepository(_databasePath))
            {
                first.SetCursor("orders", cursor);
                Assert.True(first.UpsertOrder(order, "OMG-12345"));
                first.MarkOrderSubmitted(order.Id);
            }

            using (var second = new IntegrationRepository(_databasePath))
            {
                Assert.Equal(cursor, second.GetCursor("orders"));
                var restored = second.FindOrderByCloud9Number("OMG-12345");
                Assert.NotNull(restored);
                Assert.Equal("submitted", restored.Status);
                Assert.Equal("12345", restored.OmgOrderId);
            }
        }

        [Fact]
        public void ChangedOrderResetsFailedStateAndNeedsSubmission()
        {
            using (var repository = new IntegrationRepository(_databasePath))
            {
                var original = CreateOrder("2026-08-30T10:00:00Z");
                repository.UpsertOrder(original, "OMG-12345");
                repository.MarkOrderFailed(original.Id, "temporary failure");

                var updated = CreateOrder("2026-08-30T11:00:00Z");
                Assert.True(repository.UpsertOrder(updated, "OMG-12345"));
                Assert.Single(repository.GetRetryableOrders(10));
            }
        }

        [Fact]
        public void FailedCallbackCanBeReservedAgain()
        {
            using (var repository = new IntegrationRepository(_databasePath))
            {
                var first = repository.ReserveShipmentEvent("event-1", "12345", "TRACK", 1050, "{}");
                repository.MarkShipmentFailed("event-1", "temporary failure");
                var retry = repository.ReserveShipmentEvent("event-1", "12345", "TRACK", 1050, "{}");

                Assert.True(first.IsNew);
                Assert.True(retry.IsNew);
                Assert.Equal("failed", retry.ExistingStatus);
            }
        }

        public void Dispose()
        {
            DeleteIfPresent(_databasePath);
            DeleteIfPresent(_databasePath + "-log");
        }

        private static OmgOrder CreateOrder(string updatedAt)
        {
            return OmgOrder.FromJson(JObject.Parse(@"{
                'order_id': 12345,
                'status': 'new',
                'updated_at': '" + updatedAt + @"',
                'grand_total': '85.50',
                'shipping_contact': {
                    'first_name': 'Jane', 'last_name': 'Doe',
                    'email': 'jane@example.test', 'phone': '555-0100'
                },
                'shipping_address': {
                    'first_address': '100 Main St', 'city': 'Austin',
                    'state': 'TX', 'country': 'US', 'zip': '78701'
                },
                'line_items': [{ 'id': 77, 'quantity': 2 }]
            }"));
        }

        private static void DeleteIfPresent(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
