using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VeeamTestArchiver
{
    /// <summary>
    /// Блок архиватора для упаковки или распаковки.
    /// </summary>
    internal class CompressionBlock
    {
        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="CompressionBlock"/>
        /// </summary>
        /// <param name="blockIndex">
        /// Индексный номер блока.
        /// </param>
        /// <param name="data">
        /// Данные блока.
        /// </param>
        public CompressionBlock(int blockIndex, byte[] data)
        {
            BlockIndex = blockIndex;

            Data = data;

            EffectiveSize = data.Length;
        }

        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="CompressionBlock"/>
        /// </summary>
        /// <param name="blockIndex">
        /// Индексный номер блока.
        /// </param>
        /// <param name="data">
        /// Данные блока.
        /// </param>
        /// <param name="effectiveSize">
        /// Размер полезных данных в блоке.
        /// </param>
        public CompressionBlock(int blockIndex, byte[] data, int effectiveSize)
        {
            BlockIndex = blockIndex;

            Data = data;

            EffectiveSize = effectiveSize;
        }

        /// <summary>
        /// Индексный номер блока.
        /// </summary>
        public int BlockIndex { get; private set; }

        /// <summary>
        /// Данные блока.
        /// </summary>
        public byte[] Data { get; private set; }

        /// <summary>
        /// Размер полезных данных в блоке.
        /// </summary>
        public int EffectiveSize { get; private set; }
    }
}
