using Gelf4Net.Appender;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework.Legacy;

namespace Gelf4Net.Tests.Appender
{
    /// <summary>
    /// Summary description for UnitTest1
    /// </summary>
    [TestFixture]
    public class GelfUdpAppenderTest
    {
        [Test]
        public void GenerateMessageId_TestLength()
        {
            const int expectedLength = 8;
            var actual = GelfUdpAppender.GenerateMessageId();
            ClassicAssert.AreEqual(actual.Length, expectedLength);
        }

        [Test]
        public void GenerateMessageId_NoCollision()
        {
            // Arrange
            int maxIterations = 10000;
            var generatedIds = new Dictionary<long, long>();

            // Act
            for (var i = 0; i < maxIterations; i++)
            {
                var id = BitConverter.ToInt64(GelfUdpAppender.GenerateMessageId(), 0);
                generatedIds.Add(id, id);
            }

            // Assert
            ClassicAssert.That(generatedIds.Count, Is.EqualTo(maxIterations));
        }

        [Test]
        public void CreateChunkedMessagePart_StartsWithCorrectHeader()
        {
            // Arrange
            byte[] messageId = Encoding.UTF8.GetBytes("A1B2C3D4");
            int index = 1;
            int chunkCount = 1;

            // Act
            byte[] result = GelfUdpAppender.CreateChunkedMessagePart(messageId, index, chunkCount);

            // Assert
            ClassicAssert.That(result[0], Is.EqualTo(30));
            ClassicAssert.That(result[1], Is.EqualTo(15));
        }

        [Test]
        public void CreateChunkedMessagePart_ContainsMessageId()
        {
            // Arrange
            byte[] messageId = Encoding.UTF8.GetBytes("A1B2C3D4");
            int index = 1;
            int chunkCount = 1;

            // Act
            byte[] result = GelfUdpAppender.CreateChunkedMessagePart(messageId, index, chunkCount);

            // Assert
            ClassicAssert.That(result[2], Is.EqualTo((int)'A'));
            ClassicAssert.That(result[3], Is.EqualTo((int)'1'));
            ClassicAssert.That(result[4], Is.EqualTo((int)'B'));
            ClassicAssert.That(result[5], Is.EqualTo((int)'2'));
            ClassicAssert.That(result[6], Is.EqualTo((int)'C'));
            ClassicAssert.That(result[7], Is.EqualTo((int)'3'));
            ClassicAssert.That(result[8], Is.EqualTo((int)'D'));
            ClassicAssert.That(result[9], Is.EqualTo((int)'4'));
        }

        [Test]
        public void CreateChunkedMessagePart_EndsWithIndexAndCount()
        {
            // Arrange
            byte[] messageId = Encoding.UTF8.GetBytes("A1B2C3D4");
            int index = 1;
            int chunkCount = 2;

            // Act
            byte[] result = GelfUdpAppender.CreateChunkedMessagePart(messageId, index, chunkCount);

            // Assert
            ClassicAssert.That(result[10], Is.EqualTo(index));
            ClassicAssert.That(result[11], Is.EqualTo(chunkCount));
        }
    }
}
