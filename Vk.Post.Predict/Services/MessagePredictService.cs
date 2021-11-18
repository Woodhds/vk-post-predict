﻿using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.ML;
using Vk.Post.Predict.Entities;
using Vk.Post.Predict.Models;
using Vk.Post.Predict.Service;
using MessagePredictRequest = Vk.Post.Predict.Service.MessagePredictRequest;
using MessagePredictResponse = Vk.Post.Predict.Service.MessagePredictResponse;

namespace Vk.Post.Predict.Services
{
    public class MessagePredictService : PredictService.PredictServiceBase
    {
        private readonly PredictionEnginePool<VkMessageML, VkMessagePredict> _predictionEnginePool;
        private readonly IMessageService _messageService;

        public MessagePredictService(PredictionEnginePool<VkMessageML, VkMessagePredict> predictionEnginePool, IMessageService messageService)
        {
            _predictionEnginePool = predictionEnginePool;
            _messageService = messageService;
        }


        public override async Task<MessagePredictResponse> Predict(MessagePredictRequest request, ServerCallContext context)
        {
            var messages = await _messageService.GetMessages(request.Messages.Select(f => new MessageId(f.Id, f.OwnerId)).ToArray());

            var predicted = request.Messages.Select(f => new
            {
                f.Id,
                f.OwnerId,
                Category = _predictionEnginePool.Predict(new VkMessageML { Text = f.Text, OwnerId = f.OwnerId, Id = f.Id })
            });

            return new MessagePredictResponse
            {
                Messages = {
                    request.Messages.GroupJoin(messages, 
                    a => new { a.Id, a.OwnerId }, 
                    a => new { a.Id, a.OwnerId }, 
                    (e, y) => new MessagePredictResponse.Types.MessagePredicted
                    {
                        Id = e.Id,
                        OwnerId = e.OwnerId,
                        Category = y.Select(f => f.Category).FirstOrDefault() ?? _predictionEnginePool.Predict(new VkMessageML { Text = e.Text, OwnerId = e.OwnerId, Id = e.Id })?.Category
                    })
                }
            };
        }
    }
}
