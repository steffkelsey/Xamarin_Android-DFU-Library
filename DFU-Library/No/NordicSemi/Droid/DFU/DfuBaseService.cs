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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Java.Util;
using Android.Bluetooth;
using Java.IO;

namespace No.NordicSemi.Droid.DFU
{
    /**
     * The DFU Service provides full support for Over-the-Air (OTA) Device Firmware Update (DFU) by Nordic Semiconductor.
     * With the Soft Device 7.0.0+ it allows to upload a new Soft Device, new Bootloader and a new Application. For older soft devices only the Application update is supported.
     * <p>
     * To run the service to your application extend it in your project and overwrite the missing method. Remember to add your class to the AndroidManifest.xml file.
     * </p>
     * <p>
     * Start the service with the following parameters:
     * <p/>
     * <pre>
     * readonly Intent service = new Intent(this, YourDfuService.class);
     * service.putExtra(DfuService.ExtraDeviceAddress, mSelectedDevice.getAddress()); // Target device address
     * service.putExtra(DfuService.ExtraDeviceName, mSelectedDevice.getName()); // This name will be shown on the notification
     * service.putExtra(DfuService.ExtraFileMimeType, mFileType == DfuService.TypeAuto ? YourDfuService.MimeTypeZip : YourDfuService.MimeTypeOctetStream);
     * service.putExtra(DfuService.ExtraFileType, mFileType);
     * service.putExtra(DfuService.ExtraFilePath, mFilePath);
     * service.putExtra(DfuService.ExtraFileUri, mFileStreamUri);
     * // optionally
     * service.putExtra(DfuService.ExtraInitFilePath, mInitFilePath);
     * service.putExtra(DfuService.ExtraInitFileUri, mInitFileStreamUri);
     * service.putExtra(DfuService.ExtraRestoreBond, mRestoreBond);
     * StartService(service);
     * </pre>
     * <p/>
     * The {@link #ExtraFileMimeType} and {@link #ExtraFileType} parameters are optional. If not provided the application upload from HEX/BIN file is assumed.
     * The service API is compatible with previous versions.
     * </p>
     * <p>
     * The service will show its progress on the notification bar and will send local broadcasts to the application.
     * </p>
     */
    public class DfuBaseService
    {
        /**
	     * The address of the device to update.
	     */
	    public static readonly String ExtraDeviceAddress = "No.NordicSemi.Doid.DFU.extra.ExtraDeviceAddress";
	    /**
	     * The optional device name. This name will be shown in the notification.
	     */
	    public static readonly String ExtraDeviceName = "No.NordicSemi.Doid.DFU.extra.ExtraDeviceName";
	    /**
	     * <p>
	     * If the new firmware (application) does not share the bond information with the old one, the bond information is lost. Set this flag to <code>true</code>
	     * to make the service create new bond with the new application when the upload is done (and remove the old one). When set to <code>false</code> (default),
	     * the DFU service assumes that the LTK is shared between them. Note: currently it is not possible to remove the old bond without creating a new one so if
	     * your old application supported bonding while the new one does not you have to modify the source code yourself.
	     * </p>
	     * <p>
	     * In case of updating the soft device the application is always removed together with the bond information.
	     * </p>
	     * <p>
	     * Search for occurrences of ExtraRestoreBond in this file to check the implementation and get more details.
	     * </p>
	     */
	    public static readonly String ExtraRestoreBond = "No.NordicSemi.Doid.DFU.extra.ExtraRestoreBond";
	    /**
	     * <p>This flag indicated whether the bond information should be kept or removed after an upgrade of the Application.
	     * If an application is being updated on a bonded device with the DFU Bootloader that has been configured to preserve the bond information for the new application,
	     * set it to <code>true</code>.</p>
	     *
	     * <p>By default the DFU Bootloader clears the whole application's memory. It may be however configured in the \Nordic\nrf51\components\libraries\bootloaderDfu\dfu_types.h
	     * file (line 56: <code>#define DFU_APP_DATA_RESERVED 0x0000</code>) to preserve some pages. The BLE_APP_HRMDfu sample app stores the LTK and System Attributes in the first
	     * two pages, so in order to preserve the bond information this value should be changed to 0x0800 or more.
	     * When those data are preserved, the new Application will notify the app with the Service Changed indication when launched for the first time. Otherwise this
	     * service will remove the bond information from the phone and force to refresh the device cache (see {@link #refreshDeviceCache(android.bluetooth.BluetoothGatt, Boolean)}).</p>
	     *
	     * <p>In contrast to {@link #ExtraRestoreBond} this flag will not remove the old bonding and recreate a new one, but will keep the bond information untouched.</p>
	     * <p>The default value of this flag is <code>false</code></p>
	     */
	    public static readonly String ExtraKeepBond = "No.NordicSemi.Doid.DFU.extra.ExtraKeepBond";
	    /**
	     * A path to the file with the new firmware. It may point to a HEX, BIN or a ZIP file.
	     * Some file manager applications return the path as a String while other return a Uri. Use the {@link #ExtraFileUri} in the later case.
	     */
	    public static readonly String ExtraFilePath = "No.NordicSemi.Doid.DFU.extra.ExtraFilePath";
	    /**
	     * See {@link #ExtraFilePath} for details.
	     */
	    public static readonly String ExtraFileUri = "No.NordicSemi.Doid.DFU.extra.ExtraFileUri";
	    /**
	     * The Init packet URI. This file is required if the Extended Init Packet is required (SDK 7.0+). Must point to a 'dat' file corresponding with the selected firmware.
	     * The Init packet may contain just the CRC (in case of older versions of DFU) or the Extended Init Packet in binary format (SDK 7.0+).
	     */
	    public static readonly String ExtraInitFilePath = "No.NordicSemi.Doid.DFU.extra.ExtraInitFilePath";
	    /**
	     * The Init packet URI. This file is required if the Extended Init Packet is required (SDK 7.0+). Must point to a 'dat' file corresponding with the selected firmware.
	     * The Init packet may contain just the CRC (in case of older versions of DFU) or the Extended Init Packet in binary format (SDK 7.0+).
	     */
	    public static readonly String ExtraInitFileUri = "No.NordicSemi.Doid.DFU.extra.ExtraInitFileUri";
	    /**
	     * The input file mime-type. Currently only "application/zip" (ZIP) or "application/octet-stream" (HEX or BIN) are supported. If this parameter is
	     * empty the "application/octet-stream" is assumed.
	     */
	    public static readonly String ExtraFileMimeType = "No.NordicSemi.Doid.DFU.extra.EXTRA_MIME_TYPE";
	    // Since the DFU Library version 0.5 both HEX and BIN files are supported. As both files have the same MIME TYPE the distinction is made based on the file extension.
	    public static readonly String MimeTypeOctetStream = "application/octet-stream";
	    public static readonly String MimeTypeZip = "application/zip";
	    /**
	     * This optional extra parameter may contain a file type. Currently supported are:
	     * <ul>
	     * <li>{@link #TypeSoftDevice} - only Soft Device update</li>
	     * <li>{@link #TypeBootloader} - only Bootloader update</li>
	     * <li>{@link #TypeApplication} - only application update</li>
	     * <li>{@link #TypeAuto} - the file is a ZIP file that may contain more than one HEX/BIN + DAT files. Since SDK 8.0 the ZIP Distribution packet is a recommended
	     * way of delivering firmware files. Please, see the DFU documentation for more details. A ZIP distribution packet may be created using the 'nrf utility'
	     * command line application, that is a part of Master Control Panel 3.8.0.The ZIP file MAY contain only the following files:
	     * <b>softdevice.hex/bin</b>, <b>bootloader.hex/bin</b>, <b>application.hex/bin</b> to determine the type based on its name. At lease one of them MUST be present.
	     * </li>
	     * </ul>
	     * If this parameter is not provided the type is assumed as follows:
	     * <ol>
	     * <li>If the {@link #ExtraFileMimeType} field is <code>null</code> or is equal to {@value #MimeTypeOctetStream} - the {@link #TypeApplication} is assumed.</li>
	     * <li>If the {@link #ExtraFileMimeType} field is equal to {@value #MimeTypeZip} - the {@link #TypeAuto} is assumed.</li>
	     * </ol>
	     */
	    public static readonly String ExtraFileType = "No.NordicSemi.Doid.DFU.extra.ExtraFileType";
	    /**
	     * <p>
	     * The file contains a new version of Soft Device.
	     * </p>
	     * <p>
	     * Since DFU Library 7.0 all firmware may contain an Init packet. The Init packet is required if Extended Init Packet is used by the DFU bootloader (SDK 7.0+)..
	     * The Init packet for the bootloader must be placed in the .dat file.
	     * </p>
	     *
	     * @see #ExtraFileType
	     */
	    public static readonly int TypeSoftDevice = 0x01;
	    /**
	     * <p>
	     * The file contains a new version of Bootloader.
	     * </p>
	     * <p>
	     * Since DFU Library 7.0 all firmware may contain an Init packet. The Init packet is required if Extended Init Packet is used by the DFU bootloader (SDK 7.0+).
	     * The Init packet for the bootloader must be placed in the .dat file.
	     * </p>
	     *
	     * @see #ExtraFileType
	     */
	    public static readonly int TypeBootloader = 0x02;
	    /**
	     * <p>
	     * The file contains a new version of Application.
	     * </p>
	     * <p>
	     * Since DFU Library 0.5 all firmware may contain an Init packet. The Init packet is required if Extended Init Packet is used by the DFU bootloader (SDK 7.0+).
	     * The Init packet for the application must be placed in the .dat file.
	     * </p>
	     *
	     * @see #ExtraFileType
	     */
	    public static readonly int TypeApplication = 0x04;
	    /**
	     * <p>
	     * A ZIP file that consists of more than 1 file. Since SDK 8.0 the ZIP Distribution packet is a recommended way of delivering firmware files. Please, see the DFU documentation for
	     * more details. A ZIP distribution packet may be created using the 'nrf utility' command line application, that is a part of Master Control Panel 3.8.0.
	     * For backwards compatibility this library supports also ZIP files without the manifest file. Instead they must follow the fixed naming convention:
	     * The names of files in the ZIP must be: <b>softdevice.hex</b> (or .bin), <b>bootloader.hex</b> (or .bin), <b>application.hex</b> (or .bin) in order
	     * to be read correctly. Using the Soft Device v7.0.0+ the Soft Device and Bootloader may be updated and sent together. In case of additional application file included,
	     * the service will try to send Soft Device, Bootloader and Application together (which is not supported currently) and if it fails, send first SD+BL, reconnect and send the application
	     * in the following connection.
	     * </p>
	     * <p>
	     * Since the DFU Library 0.5 you may specify the Init packet, that will be send prior to the firmware. The Init packet contains some verification data, like a device type and
	     * revision, application version or a list of supported Soft Devices. The Init packet is required if Extended Init Packet is used by the DFU bootloader (SDK 7.0+).
	     * In case of using the compatibility ZIP files the Init packet for the Soft Device and Bootloader must be in the 'system.dat' file while for the application
	     * in the 'application.dat' file (included in the ZIP). The CRC in the 'system.dat' must be a CRC of both BIN contents if both a Soft Device and a Bootloader is present.
	     * </p>
	     *
	     * @see #ExtraFileType
	     */
	    public static readonly int TypeAuto = 0x00;
	    /**
	     * An extra field with progress and error information used in broadcast events.
	     */
	    public static readonly String ExtraData = "No.NordicSemi.Doid.DFU.extra.ExtraData";
	    /**
	     * An extra field to send the progress or error information in the DFU notification. The value may contain:
	     * <ul>
	     * <li>Value 0 - 100 - percentage progress value</li>
	     * <li>One of the following status constants:
	     * <ul>
	     * <li>{@link #ProgressConnecting}</li>
	     * <li>{@link #ProgressStarting}</li>
	     * <li>{@link #ProgressEnablingDfuMode}</li>
	     * <li>{@link #ProgressValidating}</li>
	     * <li>{@link #ProgressDisconnecting}</li>
	     * <li>{@link #ProgressCompleted}</li>
	     * <li>{@link #ProgressAborted}</li>
	     * </ul>
	     * </li>
	     * <li>An error code with {@link #ErrorMask} if Initialization error occurred</li>
	     * <li>An error code with {@link #ErrorRemoteMask} if remote DFU target returned an error</li>
	     * <li>An error code with {@link #ErrorConnectionMask} if connection error occurred (f.e. GATT error (133) or Internal GATT Error (129))</li>
	     * </ul>
	     * To check if error occurred use:<br />
	     * {@code Boolean error = progressValue >= DfuBaseService.ErrorMask;}
	     */
	    public static readonly String ExtraProgress = "No.NordicSemi.Doid.DFU.extra.ExtraProgress";
	    /**
	     * The number of currently transferred part. The SoftDevice and Bootloader may be send together as one part. If user wants to upload them together with an application it has to be sent
	     * in another connection as the second part.
	     *
	     * @see No.NordicSemi.Doid.DFU.DfuBaseService#ExtraPartsTotal
	     */
	    public static readonly String ExtraPartCurrent = "No.NordicSemi.Doid.DFU.extra.ExtraPartCurrent";
	    /**
	     * Number of parts in total.
	     *
	     * @see No.NordicSemi.Doid.DFU.DfuBaseService#ExtraPartCurrent
	     */
	    public static readonly String ExtraPartsTotal = "No.NordicSemi.Doid.DFU.extra.ExtraPartsTotal";
	    /**
	     * The current upload speed in bytes/millisecond.
	     */
	    public static readonly String ExtraSpeedBPerMs = "No.NordicSemi.Doid.DFU.extra.ExtraSpeedBPerMs";
	    /**
	     * The average upload speed in bytes/millisecond for the current part.
	     */
	    public static readonly String ExtraAvgSpeedBPerMs = "No.NordicSemi.Doid.DFU.extra.ExtraAvgSpeedBPerMs";
	    /**
	     * The broadcast message contains the following extras:
	     * <ul>
	     * <li>{@link #ExtraData} - the progress value (percentage 0-100) or:
	     * <ul>
	     * <li>{@link #ProgressConnecting}</li>
	     * <li>{@link #ProgressStarting}</li>
	     * <li>{@link #ProgressEnablingDfuMode}</li>
	     * <li>{@link #ProgressValidating}</li>
	     * <li>{@link #ProgressDisconnecting}</li>
	     * <li>{@link #ProgressCompleted}</li>
	     * <li>{@link #ProgressAborted}</li>
	     * </ul>
	     * </li>
	     * <li>{@link #ExtraDeviceAddress} - the target device address</li>
	     * <li>{@link #ExtraPartCurrent} - the number of currently transmitted part</li>
	     * <li>{@link #ExtraPartsTotal} - total number of parts that are being sent, f.e. if a ZIP file contains a Soft Device, a Bootloader and an Application,
	     * the SoftDevice and Bootloader will be send together as one part. Then the service will disconnect and reconnect to the new Bootloader and send the
	     * application as part number two.</li>
	     * <li>{@link #ExtraSpeedBPerMs} - current speed in bytes/millisecond as float</li>
	     * <li>{@link #ExtraAvgSpeedBPerMs} - the average transmission speed in bytes/millisecond as float</li>
	     * </ul>
	     */
	    public static readonly String BroadcartProgress = "No.NordicSemi.Doid.DFU.broadcast.BroadcartProgress";
	    /**
	     * Service is connecting to the remote DFU target.
	     */
	    public static readonly int ProgressConnecting = -1;
	    /**
	     * Service is enabling _notifications and Starting transmission.
	     */
	    public static readonly int ProgressStarting = -2;
	    /**
	     * Service has triggered a switch to bootloader mode. Now the service waits for the link loss event (this may take up to several seconds) and will connect again
	     * to the same device, now Started in the bootloader mode.
	     */
	    public static readonly int ProgressEnablingDfuMode = -3;
	    /**
	     * Service is sending validation request to the remote DFU target.
	     */
	    public static readonly int ProgressValidating = -4;
	    /**
	     * Service is disconnecting from the DFU target.
	     */
	    public static readonly int ProgressDisconnecting = -5;
	    /**
	     * The connection is successful.
	     */
	    public static readonly int ProgressCompleted = -6;
	    /**
	     * The upload has been aborted. Previous software version will be restored on the target.
	     */
	    public static readonly int ProgressAborted = -7;
	    /**
	     * The broadcast error message contains the following extras:
	     * <ul>
	     * <li>{@link #ExtraData} - the error number. Use {@link GattError#parse(int)} to get String representation</li>
	     * <li>{@link #ExtraDeviceAddress} - the target device address</li>
	     * </ul>
	     */
	    public static readonly String BroadcastError = "No.NordicSemi.Doid.DFU.broadcast.BroadcastError";
	    /**
	     * The type of the error. This extra contains information about that kind of error has occurred. Connection state errors and other errors may share the same numbers.
	     * For example, the {@link BluetoothGattCallback#onCharacteristicWrite(BluetoothGatt, BluetoothGattCharacteristic, int)} method may return a status code 8 (GATT INSUF AUTHORIZATION),
	     * while the status code 8 returned by {@link BluetoothGattCallback#onConnectionStateChange(BluetoothGatt, int, int)} is a GATT CONN TIMEOUT error.
	     */
	    public static readonly String ExtraErrorType = "No.NordicSemi.Doid.DFU.extra.ExtraErrorType";
	    public static readonly int ErrorTypeOther = 0;
	    public static readonly int ErrorTypeCommunicationState = 1;
	    public static readonly int ErrorTypeCommunication = 2;
	    public static readonly int ErrorTypeDfuRemote = 3;
	    /**
	     * If this bit is set than the progress value indicates an error. Use {@link GattError#parse(int)} to obtain error name.
	     */
	    public static readonly int ErrorMask = 0x1000;
	    public static readonly int ErrorDeviceDisconnected = ErrorMask; // | 0x00;
	    public static readonly int ErrorFileNotFound = ErrorMask | 0x01;
	    /**
	     * Thrown if service was unable to open the file ({@link java.io.IOException} has been thrown).
	     */
	    public static readonly int ErrorFileError = ErrorMask | 0x02;
	    /**
	     * Thrown then input file is not a valid HEX or ZIP file.
	     */
	    public static readonly int ErrorFileInvalid = ErrorMask | 0x03;
	    /**
	     * Thrown when {@link java.io.IOException} occurred when reading from file.
	     */
	    public static readonly int ErrorFileIOxception = ErrorMask | 0x04;
	    /**
	     * Error thrown then {@code gatt.discoverServices();} returns false.
	     */
	    public static readonly int ErrorServiceDiscoveryNotStarted = ErrorMask | 0x05;
	    /**
	     * Thrown when the service discovery has finished but the DFU service has not been found. The device does not support DFU of is not in DFU mode.
	     */
	    public static readonly int ErrorServiceNotFound = ErrorMask | 0x06;
	    /**
	     * Thrown when the required DFU service has been found but at least one of the DFU characteristics is absent.
	     */
	    public static readonly int ErrorCharacteristicsNotFound = ErrorMask | 0x07;
	    /**
	     * Thrown when unknown response has been obtained from the target. The DFU target must follow specification.
	     */
	    public static readonly int ErrorInvalidResponse = ErrorMask | 0x08;
	    /**
	     * Thrown when the the service does not support given type or mime-type.
	     */
	    public static readonly int ErrorFileTypeUnsupported = ErrorMask | 0x09;
	    /**
	     * Thrown when the the Bluetooth adapter is disabled.
	     */
	    public static readonly int ErrorBluetoothDisabled = ErrorMask | 0x0A;
	    /**
	     * Flag set then the DFU target returned a DFU error. Look for DFU specification to get error codes.
	     */
	    public static readonly int ErrorRemoteMask = 0x2000;
	    /**
	     * The flag set when one of {@link android.bluetooth.BluetoothGattCallback} methods was called with status other than {@link android.bluetooth.BluetoothGatt#GATT_SUCCESS}.
	     */
	    public static readonly int ErrorConnectionMask = 0x4000;
	    /**
	     * The flag set when the {@link android.bluetooth.BluetoothGattCallback#onConnectionStateChange(android.bluetooth.BluetoothGatt, int, int)} method was called with
	     * status other than {@link android.bluetooth.BluetoothGatt#GATT_SUCCESS}.
	     */
	    public static readonly int ErrorConnectionStateMask = 0x8000;
	    /**
	     * The log events are only broadcast when there is no nRF Logger installed. The broadcast contains 2 extras:
	     * <ul>
	     * <li>{@link #ExtraLogLevel} - The log level, one of following: {@link #LogLevelDebug}, {@link #LogLevelVerbose}, {@link #LogLevelInfo},
	     * {@link #LogLevelApplication}, {@link #LogLevelWarning}, {@link #LogLevelError}</li>
	     * <li>{@link #ExtraLogMessage}</li> - The log message
	     * </ul>
	     */
	    public static readonly String BroadcastLog = "No.NordicSemi.Doid.DFU.broadcast.BroadcastLog";
	    public static readonly String ExtraLogMessage = "No.NordicSemi.Doid.DFU.extra.EXTRA_LOG_INFO";
	    public static readonly String ExtraLogLevel = "No.NordicSemi.Doid.DFU.extra.ExtraLogLevel";
	    /*
	     * Note:
	     * The nRF Logger API library has been excluded from the DfuLibrary.
	     * All log events are now being sent using local broadcasts and may be logged into nRF Logger in the app module.
	     * This is to make the Dfu module independent from logging tool.
	     *
	     * The log levels below are equal to log levels in nRF Logger API library, v 2.0.
	     * @see https://github.com/NordicSemiconductor/nRF-Logger-API
	     */
	    /**
	     * Level used just for debugging purposes. It has lowest level
	     */
	    public readonly static int LogLevelDebug = 0;
	    /**
	     * Log entries with minor importance
	     */
	    public readonly static int LogLevelVerbose = 1;
	    /**
	     * Default logging level for important entries
	     */
	    public readonly static int LogLevelInfo = 5;
	    /**
	     * Log entries level for applications
	     */
	    public readonly static int LogLevelApplication = 10;
	    /**
	     * Log entries with high importance
	     */
	    public readonly static int LogLevelWarning = 15;
	    /**
	     * Log entries with very high importance, like errors
	     */
	    public readonly static int LogLevelError = 20;
	    /**
	     * Activity may broadcast this broadcast in order to pause, resume or abort DFU process.
	     * Use {@link #ExtraAction} extra to pass the action.
	     */
	    public static readonly String BroadcastAction = "No.NordicSemi.Doid.DFU.broadcast.BroadcastAction";
	    /**
	     * The action extra. It may have one of the following values: {@link #ActionPause}, {@link #ActionResume}, {@link #ActionAbort}.
	     */
	    public static readonly String ExtraAction = "No.NordicSemi.Doid.DFU.extra.ExtraAction";
	    /** Pauses the upload. The service will wait for broadcasts with the action set to {@link #ActionResume} or {@link #ActionAbort}. */
	    public static readonly int ActionPause = 0;
	    /** Resumes the upload that has been paused before using {@link #ActionPause}. */
	    public static readonly int ActionResume = 1;
	    /**
	     * Aborts the upload. The service does not need to be paused before.
	     * After sending {@link #BroadcastAction} with extra {@link #ExtraAction} set to this value the DFU bootloader will restore the old application
	     * (if there was already an application). Be aware that uploading the Soft Device will erase the application in order to make space in the memory.
	     * In case there is no application, or the application has been removed, the DFU bootloader will be Started and user may try to send the application again.
	     * The bootloader may advertise with the address incremented by 1 to prevent caching services.
	     */
	    public static readonly int ActionAbort = 2;

	    // DFU status values
	    public static readonly int DfuStatusSuccess = 1;
	    public static readonly int DfuStatusInvalidState = 2;
	    public static readonly int DfuStatusNotSupported = 3;
	    public static readonly int DfuStatusDataSizeExceedsLimit = 4;
	    public static readonly int DfuStatusCrcError = 5;
	    public static readonly int DfuStatusOperationFailed = 6;
	    // Operation codes and packets
	    private static readonly byte _opCodeStartDfuKey = 0x01; // 1
	    private static readonly byte _opCodeInitDfuParamsKey = 0x02; // 2
	    private static readonly byte _opCodeReceiveFirmwareImageKey = 0x03; // 3
	    private static readonly byte _opCodeValidateKey = 0x04; // 4
	    private static readonly byte _opCodeActivateAndResetKey = 0x05; // 5
	    private static readonly byte _opCodeResetKey = 0x06; // 6
	    //private static readonly byte _opCodePacketReportReceivedImageSizeKey = 0x07; // 7
	    private static readonly byte _opCodePacketReceiptNotIfReqKey = 0x08; // 8
	    private static readonly byte _opCodeResponseCodeKey = 0x10; // 16
	    private static readonly byte _opCodePacketReceiptNotifKey = 0x11; // 11
	    private static readonly byte[] _opCodeStartDfu = new byte[] { _opCodeStartDfuKey, 0x00 };
	    private static readonly byte[] _opCodeInitDfuParamsStart = new byte[] { _opCodeInitDfuParamsKey, 0x00 };
	    private static readonly byte[] _opCodeInitDfuParamsComplete = new byte[] { _opCodeInitDfuParamsKey, 0x01 };
	    private static readonly byte[] _opCodeReceiveFirmwareImage = new byte[] { _opCodeReceiveFirmwareImageKey };
	    private static readonly byte[] _opCodeValidate = new byte[] { _opCodeValidateKey };
	    private static readonly byte[] _opCodeActivateAndReset = new byte[] { _opCodeActivateAndResetKey };
	    private static readonly byte[] _opCodeReset = new byte[] { _opCodeResetKey };
	    //private static readonly byte[] _opCodeReportReceivedImageSize = new byte[] { _opCodePacketReportReceivedImageSizeKey };
	    private static readonly byte[] _opCodePacketReceiptNotIfReq = new byte[] { _opCodePacketReceiptNotIfReqKey, 0x00, 0x00 };

	    // UUIDs used by the DFU
        private static long test = Convert.ToInt64(0x800000805F9B34FBL);
        private static readonly UUID _genericAttributeServiceUuid = new UUID(0x0000180100001000L, Convert.ToInt64(0x800000805F9B34FBL));
        private static readonly UUID _serviceChangedUuid = new UUID(0x00002A0500001000L, Convert.ToInt64(0x800000805F9B34FBL));
	    private static readonly UUID _dfuServiceUuid = new UUID(0x000015301212EFDEL, 0x1523785FEABCD123L);
	    private static readonly UUID _duControlPointUuid = new UUID(0x000015311212EFDEL, 0x1523785FEABCD123L);
	    private static readonly UUID _dfuPacketUuid = new UUID(0x000015321212EFDEL, 0x1523785FEABCD123L);
	    private static readonly UUID _dfuVersion = new UUID(0x000015341212EFDEL, 0x1523785FEABCD123L);
	    private static readonly UUID _clientCharacteristicConfig = new UUID(0x0000290200001000L, Convert.ToInt64(0x800000805f9b34fbL));
	    //
	    public static readonly int NotificationId = 283; // a random number
	    private static readonly int _notifications = 1;
	    private static readonly int _indications = 2;
	    private static readonly char[] _hexArray = "0123456789ABCDEF".ToCharArray();
	    private static readonly int _maxPacketSize = 20; // the maximum number of bytes in one packet is 20. May be less.
	    private readonly byte[] _buffer = new byte[_maxPacketSize];
	    /**
	     * Lock used in synchronization purposes
	     */
	    private readonly Object _lock = new Object();
	    private BluetoothAdapter _bluetoothAdapter;
	    private InputStream _inputStream;
	    private String _deviceAddress;
	    private String _deviceName;
	    /**
	     * The current connection state. If its value is > 0 than an error has occurred. Error number is a negative value of mConnectionState
	     */
	    private int _connectionState;
	    private readonly static int _stateDisconnected = 0;
	    private readonly static int _stateConnecting = -1;
	    private readonly static int _stateConnected = -2;
	    private readonly static int _stateConnectedAndReady = -3; // indicates that services were discovered
	    private readonly static int _stateDisconnecting = -4;
	    private readonly static int _stateClosed = -5;
	    /**
	     * The number of the last error that has occurred or 0 if there was no error
	     */
	    private int _error;
	    /**
	     * Flag set when we got confirmation from the device that notifications are enabled.
	     */
	    private Boolean _notificationsEnabled;
	    /**
	     * Flag set when we got confirmation from the device that Service Changed indications are enabled.
	     */
	    private Boolean _serviceChangedIndicationsEnabled;
	    /**
	     * The number of packets of firmware data to be send before receiving a new Packets receipt notification. 0 disables the packets _notifications
	     */
	    private int _packetsBeforeNotification = 10;
	    /**
	     * Size of BIN content of all hex files that are going to be transmitted.
	     */
	    private int _imageSizeInBytes;
	    /**
	     * Number of bytes transmitted.
	     */
	    private int _bytesSent;
	    /**
	     * Number of bytes confirmed by the notification.
	     */
	    private int _bytesConfirmed;
	    private int _packetsSentSinceNotification;
	    /**
	     * This value is used to calculate the current transfer speed.
	     */
	    private int _lastBytesSent;
	    /**
	     * Firmware update may require two connections: one for Soft Device and/or Bootloader upload and second for Application. This fields contains the current part number.
	     */
	    private int _partCurrent;
	    /**
	     * Total number of parts.
	     */
	    private int _partsTotal;
	    private int _fileType;
	    private long _lastProgressTime, mStartTime;
	    /**
	     * Flag sent when a request has been sent that will cause the DFU target to Reset. Often, after sending such command, Android throws a connection state error. If this flag is set the error will be
	     * ignored.
	     */
	    private Boolean _resetRequestSent;
	    /**
	     * Flag indicating whether the image size has been already transferred or not
	     */
	    private Boolean _imageSizeSent;
	    /**
	     * Flag indicating whether the Init packet has been already transferred or not
	     */
	    private Boolean _initPacketSent;
	    /**
	     * Flag indicating whether the request was Completed or not
	     */
	    private Boolean _requestCompleted;
	    /**
	     * <p>
	     * Flag set to <code>true</code> when the DFU target had send any notification with status other than {@link #DfuStatusSuccess}. Setting it to <code>true</code> will abort sending firmware and
	     * stop logging _notifications (read below for explanation).
	     * </p>
	     * <p>
	     * The onCharacteristicWrite(..) callback is written when Android puts the packet to the outgoing queue, not when it physically send the data. Therefore, in case of invalid state of the DFU
	     * target, Android will first put up to N* packets, one by one, while in fact the first will be transmitted. In case the DFU target is in an invalid state it will notify Android with a
	     * notification 10-03-02 for each packet of firmware that has been sent. However, just after receiving the first one this service will try to send the Reset command while still getting more
	     * 10-03-02 _notifications. This flag will prevent from logging "Notification received..." more than once.
	     * </p>
	     * <p>
	     * Additionally, sometimes after writing the command 6 ({@link #OpCodeReset}), Android will receive a notification and update the characteristic value with 10-03-02 and the callback for write
	     * Reset command will log "[DFU] Data written to ..., value (0x): 10-03-02" instead of "...(x0): 06". But this does not matter for the DFU process.
	     * </p>
	     * <p>
	     * N* - Value of Packet Receipt Notification, 10 by default.
	     * </p>
	     */
	    private Boolean _remoteErrorOccurred;
	    private Boolean _paused;
	    private Boolean _aborted;
    }
}