/*************************************************************************************************************************************************
 * Copyright (c) 2015, Nordic Semiconductor
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
 *
 * 1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
 *
 * 2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the
 * documentation and/or other materials provided with the distribution.
 *
 * 3. Neither the name of the copyright holder nor the names of its contributors may be used to endorse or promote products derived from this
 * software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
 * HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
 * LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
 * ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE
 * USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 ************************************************************************************************************************************************/
using Android.Bluetooth;
using No.NordicSemi.Droid.DFU.Events;
using System;

namespace No.NordicSemi.Droid.DFU
{
    public class GattCallback : BluetoothGattCallback
    {
        public event EventHandler<DeviceConnectionStateChangeEventArgs> DeviceConnectionStateChanged = delegate { };
        public event EventHandler ServicesDiscovered = delegate { };
        public event EventHandler<BluetoothGattCharacteristicEventArgs> GattOperationFailed = delegate { };
        public event EventHandler<BluetoothGattCharacteristicEventArgs> CharacteristicWrite = delegate { };
        public event EventHandler<BluetoothGattCharacteristicEventArgs> CharacteristicChanged = delegate { };
        public event EventHandler<BluetoothGattCharacteristicEventArgs> CharacteristicRead = delegate { };
        public event EventHandler<BluetoothGattCharacteristicEventArgs> DescriptorValueUpdated = delegate { };
        public GattCallback() { }

        public override void OnCharacteristicChanged(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic)
        {
            this.CharacteristicChanged(this, new BluetoothGattCharacteristicEventArgs()
            {
                Characteristic = characteristic
            });
        }

        public override void OnCharacteristicRead(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, GattStatus status)
        {
            this.CharacteristicRead(this, new BluetoothGattCharacteristicEventArgs()
            {
                Characteristic = characteristic,
                Status = status
            });
        }

        public override void OnCharacteristicWrite(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, GattStatus status)
        {
            this.CharacteristicWrite(this, new BluetoothGattCharacteristicEventArgs()
            {
                Characteristic = characteristic,
                Status = status
            });
        }

        public override void OnConnectionStateChange(BluetoothGatt gatt, GattStatus status, ProfileState newState)
        {
            DeviceConnectionStateChanged(this, new DeviceConnectionStateChangeEventArgs()
            {
                Status = status,
                NewState = newState
            });
        }

        public override void OnDescriptorRead(BluetoothGatt gatt, BluetoothGattDescriptor descriptor, GattStatus status)
        {
            throw new NotImplementedException();
        }

        public override void OnDescriptorWrite(BluetoothGatt gatt, BluetoothGattDescriptor descriptor, GattStatus status)
        {
            this.DescriptorValueUpdated(this, new BluetoothGattCharacteristicEventArgs()
            {
                Characteristic = descriptor.Characteristic,
                Status = status
            });
        }

        public override void OnReadRemoteRssi(BluetoothGatt gatt, int rssi, GattStatus status)
        {
            throw new NotImplementedException();
        }

        public override void OnReliableWriteCompleted(BluetoothGatt gatt, GattStatus status)
        {
            throw new NotImplementedException();
        }

        public override void OnServicesDiscovered(BluetoothGatt gatt, GattStatus status)
        {
            this.ServicesDiscovered(this, new EventArgs());
        }
    }
}