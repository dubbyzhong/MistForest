using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using ZLinq;

namespace MistForest.Server.Manager
{
    public class RoomManager
    {
        private readonly ConcurrentDictionary<string, Room> _roomById = new();

        public void AddRoom(Room room)
        {
            _roomById[room.Id] = room;
            room.Start();
        }

        public Room? GetById(string roomId)
        {
            return _roomById.TryGetValue(roomId, out Room? room) ? room : null;
        }

        public async Task StopAllAsync()
        {
            foreach (Room room in _roomById.Values)
            {
                room.Complete();
            }

            await Task.WhenAll(_roomById.Values.Select(room => room.Completion));

            _roomById.Clear();
        }
    }
}
